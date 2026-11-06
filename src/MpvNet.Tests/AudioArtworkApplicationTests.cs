using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MpvNet.Tests;

public sealed class AudioArtworkApplicationTests
{
    [Theory]
    [InlineData(false, false, ".wav", false)]
    [InlineData(false, false, ".ogg", false)]
    [InlineData(true, false, ".wav", false)]
    [InlineData(false, true, ".wav", false)]
    [InlineData(false, false, ".wav", true)]
    public async Task PlayerArtworkDoesNotBlockPlaybackOrOverrideExplicitVideoSettings(
        bool disableVideo, bool disableArtwork, string extension, bool slowNetwork)
    {
        using TestDirectory directory = new();
        CancellationToken token = TestContext.Current.CancellationToken;
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "plain" + extension,
            metadataArtist: slowNetwork ? "Example Artist" : null, seconds: slowNetwork ? 10 : 1);
        string data = Path.Combine(directory.Path, "data");
        string artworkDirectory = Path.Combine(data, "mpv.net", "Artwork");
        Directory.CreateDirectory(artworkDirectory);
        string oldCacheFile = Path.Combine(artworkDirectory, "old.png");
        await File.WriteAllBytesAsync(oldCacheFile, [1], token);
        File.SetLastWriteTimeUtc(oldCacheFile, DateTime.UtcNow.AddDays(-31));
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "mpvnet.conf"),
            slowNetwork ? "# Old config without an artwork option\n" : "audio-artwork-online=no\n", token);
        string pipeName = "mpvnet-artwork-test-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpvnet.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (string argument in new[] { "--config-dir=" + directory.Path, "--process-instance=multi",
            "--input-ipc-server=\\\\.\\pipe\\" + pipeName, "--vo=null", "--ao=null", "--pause=yes", "--keep-open=yes" })
            start.ArgumentList.Add(argument);
        if (disableVideo) start.ArgumentList.Add("--vid=no");
        if (disableArtwork) start.ArgumentList.Add("--audio-display=no");
        start.ArgumentList.Add(media);
        start.Environment["MPVNET_LOCAL_APPDATA"] = data;
        using var proxy = new TcpListener(IPAddress.Loopback, 0);
        if (slowNetwork)
        {
            proxy.Start();
            start.Environment["HTTPS_PROXY"] = "http://127.0.0.1:" + ((IPEndPoint)proxy.LocalEndpoint).Port;
            start.Environment["NO_PROXY"] = "";
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using Process process = Process.Start(start)!;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            int request = 0;
            async Task<JsonElement> Send(params string[] args)
            {
                int id = ++request;
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { command = args, request_id = id }).AsMemory(), timeout.Token);
                while (await reader.ReadLineAsync(timeout.Token) is { } line)
                {
                    using JsonDocument response = JsonDocument.Parse(line);
                    if (response.RootElement.TryGetProperty("request_id", out var result) && result.GetInt32() == id)
                        return response.RootElement.Clone();
                }
                throw new IOException("Artwork test IPC closed.");
            }
            async Task<JsonElement?> Get(string property)
            {
                JsonElement response = await Send("get_property", property);
                return response.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null ? data : null;
            }
            while ((await Get("path"))?.GetString() != media || (await Get("seekable"))?.GetBoolean() != true)
                await Task.Delay(20, timeout.Token);
            while (File.Exists(oldCacheFile))
                await Task.Delay(20, timeout.Token);
            if (slowNetwork)
            {
                async Task<TcpClient> WaitForArtworkLookup()
                {
                    while (true)
                    {
                        TcpClient connection = await proxy.AcceptTcpClientAsync(timeout.Token);
                        using var proxyReader = new StreamReader(connection.GetStream(), Encoding.ASCII, leaveOpen: true);
                        string? requestLine = await proxyReader.ReadLineAsync(timeout.Token);
                        if (requestLine?.StartsWith("CONNECT musicbrainz.org:443 ", StringComparison.Ordinal) == true)
                            return connection;
                        connection.Dispose();
                    }
                }

                await Send("set_property", "pause", "no");
                using TcpClient pending = await WaitForArtworkLookup();
                // Hold the connection open without answering. Playback and IPC
                // must advance while the request is still awaiting its headers.
                double initial = (await Get("time-pos"))?.GetDouble() ?? 0;
                while (((await Get("time-pos"))?.GetDouble() ?? 0) < initial + 0.15)
                    await Task.Delay(20, timeout.Token);
                Assert.Equal("none", (await Get("user-data/mpvnet/audio-artwork-source"))?.GetString());

                string next = NativePlaylistTitleTests.WriteWave(directory.Path, "next.wav", seconds: 10);
                await Send("loadfile", next, "replace");
                while ((await Get("path"))?.GetString() != next ||
                    (await Get("user-data/mpvnet/audio-artwork-source"))?.GetString() != "default")
                    await Task.Delay(20, timeout.Token);
                pending.Close();

                // A new lookup may start for the first track. Closing must
                // succeed even though the proxy leaves it pending again.
                await Send("loadfile", media, "replace");
                using TcpClient closing = await WaitForArtworkLookup();
                await Send("quit");
                await process.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, process.ExitCode);
                return;
            }
            if (disableVideo || disableArtwork)
            {
                await Task.Delay(300, timeout.Token);
                Assert.Null(await Get("current-tracks/video"));
                Assert.Equal("none", (await Get("user-data/mpvnet/audio-artwork-source"))?.GetString());
            }
            else
            {
                while ((await Get("user-data/mpvnet/audio-artwork-source"))?.GetString() != "default" ||
                    (await Get("current-tracks/video/albumart"))?.GetBoolean() != true)
                    await Task.Delay(20, timeout.Token);
                string? artwork = (await Get("current-tracks/video/external-filename"))?.GetString();
                Assert.NotNull(artwork);
                Assert.True(File.Exists(artwork));
                Assert.StartsWith(artworkDirectory, artwork);
            }
            await Send("quit");
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
