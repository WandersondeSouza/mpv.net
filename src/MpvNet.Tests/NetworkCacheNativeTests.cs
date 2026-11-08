using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MpvNet.Tests;

public sealed class NetworkCacheNativeTests
{
    [Theory]
    [InlineData("m3u8_native", false, 512, 64, 180)]
    [InlineData("http_dash_segments", false, 512, 64, 180)]
    [InlineData("m3u8_native", true, 512, 64, 180)]
    [InlineData("http_dash_segments", true, 512, 64, 180)]
    public async Task ResolvedManifestEvidenceSelectsNativeLimitsWithoutReloading(string protocol, bool live, int mib, int back, int seconds)
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "fixture.wav");
        using var player = new ResolvedPlayer(media, protocol, live);
        NativePlaylistTitleTests.CreateNativePlayer(player);
        Assert.True(player.RegisterHook("on_load", 10, 10));
        await Load(player, "https://example.invalid/service/page");
        Assert.Equal(mib * 1024L * 1024, player.GetPropertyLong("options/demuxer-max-bytes"));
        Assert.Equal(back * 1024L * 1024, player.GetPropertyLong("options/demuxer-max-back-bytes"));
        Assert.Equal(seconds, player.GetPropertyDouble("options/cache-secs"));
        Assert.Equal(seconds, player.GetPropertyDouble("options/demuxer-readahead-secs"));
        Assert.Equal(live ? 1 : 3, player.GetPropertyDouble("options/cache-pause-wait"));
        Assert.Equal(!live, player.GetPropertyBool("options/cache-pause-initial"));
        Assert.False(player.GetPropertyBool("options/cache-on-disk"));
        Assert.Equal(1, player.LoadCount);
        Assert.Equal("https://example.invalid/service/page", player.GetPropertyString("path"));
    }

    [Theory]
    [InlineData("ftp://example.invalid/media.wav", 512, 64, 180)]
    [InlineData("ftps://example.invalid/media.wav", 512, 64, 180)]
    [InlineData("sftp://example.invalid/media.wav", 512, 64, 180)]
    [InlineData("rtsp://example.invalid/live", 128, 16, 30)]
    [InlineData("rtmp://example.invalid/live", 128, 16, 30)]
    [InlineData("rtmps://example.invalid/live", 128, 16, 30)]
    [InlineData("udp://example.invalid/live", 128, 16, 30)]
    [InlineData("srt://example.invalid/live", 128, 16, 30)]
    public async Task ProtocolGroupsApplyNativeByteAndTimeCapsWithoutAdditionalStartupWait(string url, int mib, int back, int seconds)
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "fixture.wav");
        // Redirect before opening: this checks native option application, not
        // connectivity to a real FTP/RTSP/RTMP/datagram server.
        using var player = new ResolvedPlayer(media, "", null);
        NativePlaylistTitleTests.CreateNativePlayer(player);
        Assert.True(player.RegisterHook("on_load", 10, 10));
        await Load(player, url);
        Assert.Equal(mib * 1024L * 1024, player.GetPropertyLong("options/demuxer-max-bytes"));
        Assert.Equal(back * 1024L * 1024, player.GetPropertyLong("options/demuxer-max-back-bytes"));
        Assert.Equal(seconds, player.GetPropertyDouble("options/cache-secs"));
        Assert.Equal(seconds, player.GetPropertyDouble("options/demuxer-readahead-secs"));
        if (mib == 128)
        {
            Assert.False(player.GetPropertyBool("options/cache-pause-initial"));
            Assert.Equal(1, player.GetPropertyDouble("options/cache-pause-wait"));
        }
        Assert.False(player.GetPropertyBool("options/cache-on-disk"));
        Assert.Equal(1, player.LoadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpVodUsesLargerNativeCeilingAndLocalItemRestoresOriginalOptions(bool disabled)
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "local.wav", seconds: 10);
        await using var server = new LocalMediaServer(await File.ReadAllBytesAsync(media, TestContext.Current.CancellationToken));
        bool previous = Global.App.AutomaticNetworkCache;
        try
        {
            Global.App.AutomaticNetworkCache = !disabled;
            using var player = NativePlaylistTitleTests.CreateNativePlayer();
            long initial = player.GetPropertyLong("options/demuxer-max-bytes");
            string initialWait = player.GetPropertyString("options/cache-pause-wait");
            await Load(player, server.Url);
            Assert.Equal(disabled ? initial : 512L * 1024 * 1024, player.GetPropertyLong("options/demuxer-max-bytes"));
            Assert.Equal(disabled ? initialWait : "3.000000", player.GetPropertyString("options/cache-pause-wait"));
            await Load(player, media);
            Assert.Equal(initial, player.GetPropertyLong("options/demuxer-max-bytes"));
            Assert.Equal(initialWait, player.GetPropertyString("options/cache-pause-wait"));
            player.Dispose(); // Shutdown and late calls are safe with a session having existed.
            player.CommandV("stop");
        }
        finally { Global.App.AutomaticNetworkCache = previous; }
    }

    [Theory]
    [InlineData("cli")]
    [InlineData("config")]
    [InlineData("profile")]
    [InlineData("file-local")]
    [InlineData("global-script")]
    [InlineData("late-script")]
    public async Task NativeExplicitCacheOptionsAlwaysWinAcrossLoadPhases(string source)
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "fixture.wav");
        using var player = new ResolvedPlayer(media, "https", false, source);
        (string, string)[] options = source == "cli" ? [("demuxer-max-bytes", "17MiB"), ("cache-pause-wait", "9")] : [];
        if (source is "config" or "profile")
        {
            string content = source == "config" ? "demuxer-max-bytes=17MiB\ncache-pause-wait=9\n" :
                "[custom]\ndemuxer-max-bytes=17MiB\ncache-pause-wait=9\n";
            await File.WriteAllTextAsync(Path.Combine(directory.Path, "mpv.conf"), content, TestContext.Current.CancellationToken);
            options = [("config", "yes"), ("config-dir", directory.Path)];
        }
        NativePlaylistTitleTests.CreateNativePlayer(player, options);
        if (source == "profile") player.CommandV("apply-profile", "custom");
        if (source == "global-script")
        {
            player.SetPropertyString("options/demuxer-max-bytes", "17MiB");
            player.SetPropertyString("options/cache-pause-wait", "9");
        }
        Assert.True(player.RegisterHook("on_load", 10, 10));
        if (source == "late-script") Assert.True(player.RegisterHook("on_load", 11, 70));
        await Load(player, "https://example.invalid/service/page");
        Assert.Equal(17L * 1024 * 1024, player.GetPropertyLong("options/demuxer-max-bytes"));
        Assert.Equal(9, player.GetPropertyDouble("options/cache-pause-wait"));
    }

    [Fact]
    public async Task CacheOptionsReallyChangeDuringPlaybackAndRestoreAfterStop()
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "fixture.wav");
        using var player = NativePlaylistTitleTests.CreateNativePlayer();
        await Load(player, media);
        string original = player.GetPropertyString("options/cache-pause-wait");
        player.SetPropertyString("file-local-options/cache-pause-wait", "7");
        Assert.Equal(7, player.GetPropertyDouble("options/cache-pause-wait"));
        player.SetPropertyString("file-local-options/demuxer-max-bytes", "512MiB");
        Assert.Equal(512L * 1024 * 1024, player.GetPropertyLong("options/demuxer-max-bytes"));
        player.CommandV("stop");
        await NativePlaylistTitleTests.WaitUntil(() => player.GetPropertyBool("idle-active", false));
        await NativePlaylistTitleTests.WaitUntil(() => player.GetPropertyString("options/cache-pause-wait") == original);
        Assert.Equal(original, player.GetPropertyString("options/cache-pause-wait"));
        Assert.StartsWith("mpv v0.", player.GetPropertyString("mpv-version"));
    }

    [Theory]
    [InlineData("cache-secs", "demuxer-readahead-secs")]
    [InlineData("demuxer-readahead-secs", "cache-secs")]
    public async Task ExplicitTimeLimitDoesNotAcquireAnAutomaticLargerCounterpart(string name, string counterpart)
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "fixture.wav");
        using var player = new ResolvedPlayer(media, "https", false);
        NativePlaylistTitleTests.CreateNativePlayer(player, [(name, "2")]);
        string original = player.GetPropertyString("options/" + counterpart);
        Assert.True(player.RegisterHook("on_load", 10, 10));
        await Load(player, "https://example.invalid/service/page");
        Assert.Equal(2, player.GetPropertyDouble("options/" + name));
        Assert.Equal(original, player.GetPropertyString("options/" + counterpart));
    }

    [Fact]
    public async Task ExplicitDiskCacheDoesNotAcquireAutomaticByteOrTimeLimits()
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "fixture.wav");
        using var player = new ResolvedPlayer(media, "https", false);
        NativePlaylistTitleTests.CreateNativePlayer(player, [("cache-on-disk", "yes"), ("demuxer-cache-dir", directory.Path)]);
        long original = player.GetPropertyLong("options/demuxer-max-bytes");
        string time = player.GetPropertyString("options/cache-secs");
        Assert.True(player.RegisterHook("on_load", 10, 10));
        await Load(player, "https://example.invalid/service/page");
        Assert.True(player.GetPropertyBool("options/cache-on-disk"));
        Assert.Equal(original, player.GetPropertyLong("options/demuxer-max-bytes"));
        Assert.Equal(time, player.GetPropertyString("options/cache-secs"));
    }

    static async Task Load(MainPlayer player, string path)
    {
        TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded() => loaded.TrySetResult();
        player.FileLoaded += OnLoaded;
        try
        {
            player.CommandV("loadfile", path);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally { player.FileLoaded -= OnLoaded; }
    }

    sealed class ResolvedPlayer(string media, string protocol, bool? live, string source = "") : MainPlayer
    {
        internal int LoadCount;
        internal override void OnHook(MpvEventSnapshot snapshot)
        {
            if (snapshot.HookName == "on_load" && snapshot.ReplyUserData == 10)
            {
                LoadCount++;
                SetPropertyString("stream-open-filename", media);
                SetPropertyString("user-data/mpv/ytdl/json-subprocess-result/stdout",
                    JsonSerializer.Serialize(new { is_live = live, duration = 60, protocol, url = "https://signed.invalid/?token=secret" }));
            }
            if (source == "file-local" && snapshot.ReplyUserData == 10 ||
                source == "late-script" && snapshot.ReplyUserData == 11)
            {
                SetPropertyString("file-local-options/demuxer-max-bytes", "17MiB");
                SetPropertyString("file-local-options/cache-pause-wait", "9");
            }
            base.OnHook(snapshot);
        }
    }

    // Real loopback HTTP, supporting FFmpeg's byte-range requests. No external service.
    sealed class LocalMediaServer : IAsyncDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource _stop = new();
        readonly Task _serve;
        readonly byte[] _media;
        internal string Url { get; }
        internal LocalMediaServer(byte[] media)
        {
            _media = media;
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/media.wav";
            _serve = Serve();
        }
        async Task Serve()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    using NetworkStream stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    int start = 0;
                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(_stop.Token)))
                        if (line.StartsWith("Range: bytes=", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(line[13..].Split('-')[0], out start);
                    start = Math.Clamp(start, 0, _media.Length - 1);
                    string header = $"HTTP/1.1 206 Partial Content\r\nContent-Type: audio/wav\r\nAccept-Ranges: bytes\r\nContent-Range: bytes {start}-{_media.Length - 1}/{_media.Length}\r\nContent-Length: {_media.Length - start}\r\nConnection: close\r\n\r\n";
                    try
                    {
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), _stop.Token);
                        await stream.WriteAsync(_media.AsMemory(start), _stop.Token);
                    }
                    catch (IOException) { } // A seek/stop can close the client early.
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync(); _listener.Stop();
            await _serve; _stop.Dispose();
        }
    }
}
