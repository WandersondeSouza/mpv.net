using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MpvNet;
using MpvNet.Windows.Services.MediaTransport;
using Xunit;

namespace MpvNet.Tests;

public sealed class AudioArtworkTests
{
    static CancellationToken Token => TestContext.Current.CancellationToken;
    static readonly string Release = "76df3287-6cda-33eb-8e9a-044b5e15ffdd";

    static Task<byte[]> Image(int size = 2) => AudioArtworkImage.EncodeAsync(new(size, size,
        Enumerable.Range(0, size * size).SelectMany(_ => new byte[] { 0, 0, 255, 255 }).ToArray()), Token);

    [Fact]
    public void OnlineConfigurationDefaultsToOfflineAndPriorityDoesNotGrantNetworkPermission()
    {
        using AppClass app = new();
        Assert.False(app.AudioArtworkOnline);
        Assert.False(app.AudioArtworkPreferOnline);
        Assert.True(app.ProcessProperty("audio-artwork-prefer-online", "yes"));
        Assert.True(app.AudioArtworkPreferOnline);
        Assert.False(app.AudioArtworkOnline);
        Assert.True(app.ProcessProperty("audio-artwork-online", "yes"));
        Assert.True(app.AudioArtworkOnline);
        app.ProcessProperty("audio-artwork-online", "no");
        Assert.False(app.AudioArtworkOnline);
    }

    [Theory]
    [InlineData("https://musicbrainz.org/ws/2/release", true)]
    [InlineData("https://coverartarchive.org/release/id/front-500", true)]
    [InlineData("https://ia800500.us.archive.org/image.jpg", true)]
    [InlineData("http://coverartarchive.org/image", false)]
    [InlineData("https://archive.org.evil.test/image", false)]
    [InlineData("https://user:password@archive.org/image", false)]
    [InlineData("https://archive.org:8080/image", false)]
    [InlineData("https://localhost/image", false)]
    [InlineData("file:///C:/image.png", false)]
    public void OnlyExpectedHttpsArtworkDestinationsAreAccepted(string uri, bool expected) =>
        Assert.Equal(expected, OnlineAudioArtwork.IsAllowedUri(new(uri)));

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Artist", null, null)]
    [InlineData("https://user:secret@example.com", "Album", null)]
    [InlineData("Artist", "Album", "tags:ARTIST\nALBUM")]
    public void IdentityRequiresRealMetadata(string? artist, string? album, string? key) =>
        Assert.Equal(key, new AudioArtworkIdentity(artist, album).Key);

    [Fact]
    public void ValidMusicBrainzIdsTakePrecedenceAndInvalidIdsFallBackToTags()
    {
        Assert.Equal("release:" + Release, new AudioArtworkIdentity("Artist", "Album", Release).Key);
        Assert.Equal("release-group:" + Release, new AudioArtworkIdentity(null, null, "invalid", Release).Key);
        Assert.Equal("tags:ARTIST\nALBUM", new AudioArtworkIdentity("Artist", "Album", "invalid").Key);
    }

    [Theory]
    [InlineData(true, false, "embedded-first", "C:\\song.mp3", true)]
    [InlineData(true, true, "embedded-first", "C:\\song.mp3", false)]
    [InlineData(false, false, "embedded-first", "C:\\image.png", false)]
    [InlineData(true, false, "no", "C:\\song.mp3", false)]
    [InlineData(true, false, "embedded-first", "https://user:password@example.com/song", false)]
    [InlineData(true, false, "embedded-first", "\\\\server\\song.mp3", false)]
    public void OnlyLocalAudioWithoutRealVideoIsEligible(bool audio, bool video, string display, string path, bool expected) =>
        Assert.Equal(expected, AudioArtworkResolver.IsEligible(Request(path) with { HasAudio = audio, HasRealVideo = video, AudioDisplay = display }));

    [Fact]
    public async Task ImageIsFullyDecodedAndSizeDimensionsAndCancellationAreEnforced()
    {
        byte[] png = await Image();
        Assert.NotEmpty(await AudioArtworkImage.NormalizeAsync(png, Token));
        await Assert.ThrowsAsync<InvalidDataException>(() => AudioArtworkImage.NormalizeAsync(new byte[AudioArtworkImage.MaxBytes + 1], Token));
        await Assert.ThrowsAnyAsync<Exception>(() => AudioArtworkImage.NormalizeAsync(Encoding.UTF8.GetBytes("not an image"), Token));
        await Assert.ThrowsAsync<InvalidDataException>(() => AudioArtworkImage.EncodeAsync(new(4097, 1, []), Token));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AudioArtworkImage.NormalizeAsync(png, cancel.Token));
    }

    [Fact]
    public async Task LocalCoverAndEmbeddedPixelsAreOwnedCachedAndDoNotModifyTheMedia()
    {
        using TestDirectory directory = new();
        string media = Path.Combine(directory.Path, "song.mp3");
        byte[] original = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(media, original, Token);
        string cover = Path.Combine(directory.Path, "cover.png");
        await File.WriteAllBytesAsync(cover, await Image(), Token);
        var cache = new AudioArtworkCache(Path.Combine(directory.Path, "cache"));
        using var server = new Server(_ => (500, []));
        using var online = Online(cache, server);
        var resolver = new AudioArtworkResolver(cache, online);
        var local = await resolver.ResolveAsync(Request(media) with { SelectedCoverPath = cover },
            _ => throw new InvalidOperationException("External cover must not capture pixels."), Token);
        Assert.NotNull(local);
        Assert.False(local.AddToPlayer);
        File.Delete(cover);
        Assert.NotNull(await AudioArtworkImage.ReadLocalAsync(local.Path, Token));
        var embedded = await resolver.ResolveAsync(Request(media) with { HasEmbeddedCover = true },
            _ => Task.FromResult<AlbumArtworkPixels?>(new(1, 1, [255, 0, 0, 255])), Token);
        Assert.NotNull(embedded);
        Assert.NotEqual(local.Path, embedded.Path);
        Assert.Equal(original, await File.ReadAllBytesAsync(media, Token));
        Assert.Equal(0, server.Requests);
    }

    [Fact]
    public async Task DisabledNetworkExplicitSelectionAndUnsafeMediaNeverCallTheProvider()
    {
        using TestDirectory directory = new();
        string media = Path.Combine(directory.Path, "song.wav");
        await File.WriteAllBytesAsync(media, [1], Token);
        var cache = new AudioArtworkCache(Path.Combine(directory.Path, "cache"));
        using var server = new Server(_ => (500, []));
        using var online = Online(cache, server);
        var resolver = new AudioArtworkResolver(cache, online);
        foreach (var request in new[] { Request(media) with { PreferOnline = true },
            Request(media) with { OnlineEnabled = true, AllowAdd = false },
            Request(media) with { OnlineEnabled = true, HasRealVideo = true },
            Request(media) with { OnlineEnabled = true, AudioDisplay = "no" },
            Request("https://user:secret@example.com") with { OnlineEnabled = true } })
            await resolver.ResolveAsync(request, _ => Task.FromResult<AlbumArtworkPixels?>(null), Token);
        Assert.Equal(0, server.Requests);
    }

    [Fact]
    public async Task CacheReusesValidatedImagesExpiresEntriesAndRejectsTraversalAndCorruption()
    {
        using TestDirectory directory = new();
        var cache = new AudioArtworkCache(directory.Path);
        string image = await cache.StoreImageAsync(await Image(), Token);
        await cache.RememberAsync("album", image, TimeSpan.FromDays(1), Token);
        Assert.Equal((true, image), await cache.FindAsync("album", Token));
        await File.WriteAllTextAsync(image, "bad image", Token);
        Assert.False((await cache.FindAsync("album", Token)).Found);
        await cache.RememberAsync("missing", null, TimeSpan.FromDays(1), Token);
        Assert.Equal((true, (string?)null), await cache.FindAsync("missing", Token));
        await cache.RememberAsync("expired", null, TimeSpan.FromSeconds(-1), Token);
        Assert.False((await cache.FindAsync("expired", Token)).Found);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, AudioArtworkCache.Hash("traversal") + ".json"),
            JsonSerializer.Serialize(new AudioArtworkCache.Entry("..\\other", DateTimeOffset.UtcNow.AddDays(1))), Token);
        Assert.False((await cache.FindAsync("traversal", Token)).Found);
    }

    [Fact]
    public async Task CacheBoundsSpaceAndRemovesExpiredFilesWithoutTouchingConfiguration()
    {
        using TestDirectory directory = new();
        string old = Path.Combine(directory.Path, "old.png");
        await File.WriteAllBytesAsync(old, [1], Token);
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-31));
        string config = Path.Combine(directory.Path, "mpv.conf");
        await File.WriteAllTextAsync(config, "unchanged", Token);
        for (int i = 0; i < 35; i++)
            await File.WriteAllBytesAsync(Path.Combine(directory.Path, i + ".png"), new byte[AudioArtworkImage.MaxBytes], Token);
        var cache = new AudioArtworkCache(directory.Path);
        string current = await cache.StoreImageAsync(await Image(), Token);
        Assert.True(File.Exists(current));
        Assert.False(File.Exists(old));
        Assert.True(new DirectoryInfo(directory.Path).GetFiles("*.png").Sum(file => file.Length) <= AudioArtworkCache.MaxCacheBytes);
        Assert.Equal("unchanged", await File.ReadAllTextAsync(config, Token));
    }

    [Fact]
    public async Task RealHttpUsesIdsFollowsValidatedRedirectsAndCaches404AndAlbums()
    {
        using TestDirectory directory = new();
        byte[] png = await Image();
        using var server = new Server(path => path.Contains("missing") ? (404, []) : (200, png));
        var cache = new AudioArtworkCache(directory.Path);
        using var online = Online(cache, server);
        var identity = new AudioArtworkIdentity(null, null, Release);
        string? first = await online.FindAsync(identity, Token);
        Assert.NotNull(first);
        Assert.Equal(first, await online.FindAsync(identity, Token));
        Assert.Equal(1, server.Requests);
        Assert.Contains("MPV.NET/", server.UserAgent);
        Assert.Equal("/release/" + Release + "/front-500", server.LastPath);
        using var missingServer = new Server(_ => (404, []));
        using var missing = Online(cache, missingServer);
        var missingIdentity = identity with { ReleaseId = Guid.NewGuid().ToString() };
        Assert.Null(await missing.FindAsync(missingIdentity, Token));
        Assert.Null(await missing.FindAsync(missingIdentity, Token));
        Assert.Equal(1, missingServer.Requests);
    }

    [Theory]
    [InlineData("success", 2)]
    [InlineData("forbidden", 1)]
    [InlineData("loop", 5)]
    [InlineData("oversized", 1)]
    public async Task HttpRedirectsAndResponseLimitsAreEnforced(string mode, int requests)
    {
        using TestDirectory directory = new();
        byte[] png = await Image();
        using var server = new Server(path => mode == "oversized" ? (200, new byte[AudioArtworkImage.MaxBytes + 1]) :
            path == "/image" ? (200, png) : (307, []),
            headers: _ => "Location: " + (mode == "forbidden" ? "https://example.invalid/image" : mode == "loop" ? "/again" : "/image") + "\r\n");
        using var online = Online(new(directory.Path), server);
        string? result = await online.FindAsync(new(null, null, Release), Token);
        Assert.Equal(mode == "success", result != null);
        Assert.Equal(requests, server.Requests);
    }

    [Fact]
    public async Task RetryAfterDoesNotRetryImmediatelyAndCancellationInterruptsTheWait()
    {
        using TestDirectory directory = new();
        using var server = new Server(_ => (429, []), headers: _ => "Retry-After: 1\r\n");
        using var online = Online(new(directory.Path), server, TimeSpan.FromMilliseconds(250));
        Assert.Null(await online.FindAsync(new(null, null, Release), Token));
        Assert.Null(await online.FindAsync(new(null, null, Release), Token)); // Negative cache.
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            online.FindAsync(new(null, null, Guid.NewGuid().ToString()), cancel.Token));
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task ConcurrentTracksFromOneAlbumShareTheValidatedDownload()
    {
        using TestDirectory directory = new();
        byte[] png = await Image();
        using var server = new Server(_ => (200, png));
        using var online = Online(new(directory.Path), server);
        string?[] results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => online.FindAsync(new(null, null, Release), Token)));
        Assert.All(results, result => Assert.NotNull(result));
        Assert.Single(results.Distinct());
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task OnlinePreferenceIsIndependentFromOfflineAndNativeSelectionWins()
    {
        using TestDirectory directory = new();
        string media = Path.Combine(directory.Path, "audio.wav");
        string cover = Path.Combine(directory.Path, "cover.png");
        await File.WriteAllBytesAsync(media, [1], Token);
        await File.WriteAllBytesAsync(cover, await Image(), Token);
        byte[] downloaded = await AudioArtworkImage.EncodeAsync(new(1, 1, [255, 0, 0, 255]), Token);
        using var server = new Server(_ => (200, downloaded));
        var cache = new AudioArtworkCache(Path.Combine(directory.Path, "cache"));
        using var online = Online(cache, server);
        var resolver = new AudioArtworkResolver(cache, online);
        var request = Request(media) with { SelectedCoverPath = cover, OnlineEnabled = true };
        Assert.Equal("local", (await resolver.ResolveAsync(request, _ => Task.FromResult<AlbumArtworkPixels?>(null), Token))?.Source);
        Assert.Equal(0, server.Requests);
        Assert.Equal("online", (await resolver.ResolveAsync(request with { PreferOnline = true },
            _ => Task.FromResult<AlbumArtworkPixels?>(null), Token))?.Source);
        Assert.Equal(1, server.Requests);
        Assert.Equal("local", (await resolver.ResolveAsync(request with { PreferOnline = true, AllowAdd = false },
            _ => Task.FromResult<AlbumArtworkPixels?>(null), Token))?.Source);
        Assert.Equal(1, server.Requests);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(200)]
    public async Task InvalidOrFailedHttpResponsesStayOptionalAndAreNegativelyCached(int status)
    {
        using TestDirectory directory = new();
        using var server = new Server(_ => (status, Encoding.UTF8.GetBytes("HTML instead of a cover")));
        var cache = new AudioArtworkCache(directory.Path);
        using var online = Online(cache, server);
        var resolver = new AudioArtworkResolver(cache, online);
        string media = Path.Combine(directory.Path, "audio.wav");
        await File.WriteAllBytesAsync(media, [1], Token);
        var request = Request(media) with { OnlineEnabled = true };
        Assert.Equal("default", (await resolver.ResolveAsync(request, _ => Task.FromResult<AlbumArtworkPixels?>(null), Token))?.Source);
        Assert.Equal("default", (await resolver.ResolveAsync(request, _ => Task.FromResult<AlbumArtworkPixels?>(null), Token))?.Source);
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task TimeoutIsNegativelyCachedAndCallerCancellationNeverCachesAStaleImage()
    {
        using TestDirectory directory = new();
        using var server = new Server(_ => (200, []), delay: TimeSpan.FromSeconds(2));
        var cache = new AudioArtworkCache(directory.Path);
        using var online = Online(cache, server, TimeSpan.FromMilliseconds(150));
        var identity = new AudioArtworkIdentity(null, null, Release);
        Assert.Null(await online.FindAsync(identity, Token));
        Assert.Equal((true, (string?)null), await cache.FindAsync(identity.Key!, Token));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(50);
        var nextIdentity = identity with { ReleaseId = Guid.NewGuid().ToString() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => online.FindAsync(nextIdentity, cancellation.Token));
        Assert.False((await cache.FindAsync(nextIdentity.Key!, Token)).Found);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.png"));
    }

    [Theory]
    [InlineData(94, "Album", "Artist", false)]
    [InlineData(100, "Another Album", "Artist", false)]
    [InlineData(100, "Album", "Another Artist", false)]
    [InlineData(100, "Album", "Artist", true)]
    public void SearchRequiresHighConfidenceExactAlbumAndArtist(int score, string album, string artist, bool expected)
    {
        byte[] json = SearchResult(score, album, artist);
        Assert.Equal(expected ? "release/" + Release : null,
            OnlineAudioArtwork.SelectRelease(json, new("Artist", "Album")));
    }

    [Fact]
    public async Task SearchByTagsUsesEscapedMetadataAndAmbiguousAlbumsAreRejected()
    {
        using TestDirectory directory = new();
        byte[] png = await Image();
        using var server = new Server(path => path.StartsWith("/release?") ? (200, SearchResult(100, "Album", "Artist")) : (200, png));
        using var online = Online(new(directory.Path), server);
        Assert.NotNull(await online.FindAsync(new("Artist", "Album"), Token));
        Assert.Equal(2, server.Requests);
        string json = Encoding.UTF8.GetString(SearchResult(100, "Album", "Artist"));
        using JsonDocument result = JsonDocument.Parse(json);
        string item = result.RootElement.GetProperty("releases")[0].GetRawText();
        byte[] ambiguous = Encoding.UTF8.GetBytes("{\"releases\":[" + item + "," + item.Replace(Release, Guid.NewGuid().ToString()) + "]}");
        Assert.Null(OnlineAudioArtwork.SelectRelease(ambiguous, new("Artist", "Album")));
    }

    static byte[] SearchResult(int score, string album, string artist) =>
        Encoding.UTF8.GetBytes("{\"releases\":[{\"id\":\"" + Release + "\",\"score\":" + score +
        ",\"title\":" + JsonSerializer.Serialize(album) + ",\"artist-credit\":[{\"name\":" + JsonSerializer.Serialize(artist) + "}]}]}");

    static AudioArtworkRequest Request(string path) => new(path, true, false, false, null, "auto", "embedded-first",
        true, false, false, new("Artist", "Album", Release));

    static OnlineAudioArtwork Online(AudioArtworkCache cache, Server server, TimeSpan? timeout = null) => new(cache,
        new HttpClientHandler { AllowAutoRedirect = false }, uri => uri.Host == "127.0.0.1" && uri.Port == server.Address.Port,
        server.Address, server.Address, TimeSpan.Zero, timeout ?? TimeSpan.FromSeconds(5));

    sealed class Server : IDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource _stop = new();
        readonly Task _loop;
        public Uri Address { get; }
        public int Requests;
        public string UserAgent = "";
        public string LastPath = "";

        public Server(Func<string, (int Status, byte[] Body)> response, TimeSpan? delay = null, Func<string, string>? headers = null)
        {
            _listener.Start();
            Address = new("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/");
            _loop = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    using NetworkStream stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    string line = (await reader.ReadLineAsync(_stop.Token))!;
                    LastPath = line.Split(' ')[1];
                    while (!string.IsNullOrEmpty(line = (await reader.ReadLineAsync(_stop.Token))!))
                        if (line.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase))
                            UserAgent = line[11..].Trim();
                    Interlocked.Increment(ref Requests);
                    if (delay != null)
                        await Task.Delay(delay.Value, _stop.Token);
                    var (status, body) = response(LastPath);
                    byte[] header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Length: {body.Length}\r\nConnection: close\r\n{headers?.Invoke(LastPath)}\r\n");
                    try
                    {
                        await stream.WriteAsync(header, _stop.Token);
                        await stream.WriteAsync(body, _stop.Token);
                    }
                    catch (IOException) { }
                }
            });
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            try { _loop.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
            _stop.Dispose();
        }
    }
}
