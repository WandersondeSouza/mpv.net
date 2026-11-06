using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed record AudioArtworkIdentity(string? Artist, string? Album, string? ReleaseId = null, string? ReleaseGroupId = null)
{
    public string? Key => Guid.TryParse(ReleaseId, out Guid release) ? "release:" + release.ToString("D") :
        Guid.TryParse(ReleaseGroupId, out Guid group) ? "release-group:" + group.ToString("D") :
        IsTag(Artist) && IsTag(Album) ? "tags:" + Normalize(Artist!) + "\n" + Normalize(Album!) : null;

    public static string Normalize(string value) => value.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant();
    internal static bool IsTag(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 &&
        !value.Contains("://", StringComparison.Ordinal) && !value.Contains('\\') && !value.Any(char.IsControl);
}

public sealed class OnlineAudioArtwork : IDisposable
{
    static readonly SemaphoreSlim NetworkGate = new(1);
    static DateTimeOffset _nextRequest;
    readonly Dictionary<string, DateTimeOffset> _retryAfter = new(StringComparer.Ordinal);
    readonly HttpClient _client;
    readonly AudioArtworkCache _cache;
    readonly Func<Uri, bool> _allowUri;
    readonly TimeSpan _spacing;
    readonly TimeSpan _timeout;
    readonly Uri _musicBrainz;
    readonly Uri _coverArt;

    public OnlineAudioArtwork(AudioArtworkCache cache) : this(cache,
        new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }, IsAllowedUri,
        new("https://musicbrainz.org/ws/2/"), new("https://coverartarchive.org/"), TimeSpan.FromMilliseconds(1100), TimeSpan.FromSeconds(15)) { }

    // Explicit transport injection lets tests exercise real loopback HTTP without
    // weakening HTTPS/host validation in the application's constructor.
    public OnlineAudioArtwork(AudioArtworkCache cache, HttpMessageHandler handler, Func<Uri, bool> allowUri,
        Uri musicBrainz, Uri coverArt, TimeSpan spacing, TimeSpan timeout)
    {
        _cache = cache;
        _client = new(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("MPV.NET/" + AppInfo.Version + " (+https://github.com/WandersondeSouza/mpv.net)");
        _allowUri = allowUri;
        _musicBrainz = musicBrainz;
        _coverArt = coverArt;
        _spacing = spacing;
        _timeout = timeout;
    }

    public static bool IsAllowedUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
        (uri.IdnHost is "musicbrainz.org" or "coverartarchive.org" or "archive.org" ||
         uri.IdnHost.EndsWith(".archive.org", StringComparison.Ordinal));

    public Task<string?> FindAsync(AudioArtworkIdentity identity, CancellationToken token) =>
        FindAsync(identity, true, token);

    public Task<string?> FindAsync(AudioArtworkIdentity identity, bool networkEnabled, CancellationToken token) =>
        FindAsync(identity.Key, networkEnabled, null, async cancellation =>
        {
            string? resource = await ResolveResourceAsync(identity, cancellation).ConfigureAwait(false);
            byte[]? image = resource == null ? null : await GetAsync(new(_coverArt, resource + "/front-500"),
                AudioArtworkImage.MaxBytes, cancellation).ConfigureAwait(false);
            return (image, null);
        }, token);

    public Task<string?> FindArtistAsync(AudioArtistIdentity artist, bool networkEnabled, CancellationToken token) =>
        FindAsync(artist.Key, networkEnabled, artist.Matches, cancellation => ArtistArtworkLookup.ResolveAsync(
            artist, _musicBrainz, (uri, limit) => GetAsync(uri, limit, cancellation, allowRedirects: false)), token);

    async Task<string?> FindAsync(string? key, bool networkEnabled, Func<ArtistArtworkSource, bool>? validateArtist,
        Func<CancellationToken, Task<(byte[]? Image, ArtistArtworkSource? Artist)>> resolve, CancellationToken token)
    {
        if (key == null)
            return null;
        if (!networkEnabled)
            return (await _cache.FindAsync(key, validateArtist, token).ConfigureAwait(false)).Path;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_timeout);
        CancellationToken requestToken = timeout.Token;
        // Serialize the cache check and request, coalescing consecutive tracks
        // from the same album. Cancellation releases the gate immediately.
        try { await NetworkGate.WaitAsync(requestToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Log.Debug("Online audio artwork wait timed out.");
            return null;
        }
        try
        {
            var cached = await _cache.FindAsync(key, validateArtist, requestToken).ConfigureAwait(false);
            if (cached.Found)
                return cached.Path;
            var (bytes, artist) = await resolve(requestToken).ConfigureAwait(false);
            string? image = bytes == null ? null : await _cache.StoreImageAsync(
                await AudioArtworkImage.NormalizeAsync(bytes, requestToken).ConfigureAwait(false), requestToken).ConfigureAwait(false);
            if (image == null)
                Log.Debug(validateArtist == null ? "Album artwork unavailable." : "Artist artwork unavailable or unverified.");
            await _cache.RememberAsync(key, image, image == null ? TimeSpan.FromDays(1) : TimeSpan.FromDays(30),
                image == null ? null : artist, requestToken).ConfigureAwait(false);
            return image;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
        {
            Log.Debug($"Online audio artwork unavailable: {ex.GetType().Name}");
            // Cache transient failures briefly as well; a playlist of one album
            // must not hammer an unavailable service once per track.
            try { await _cache.RememberAsync(key, null, TimeSpan.FromMinutes(5), token).ConfigureAwait(false); }
            catch (Exception cacheError) when (cacheError is IOException or UnauthorizedAccessException)
            {
                Log.Debug($"Artwork cache unavailable: {cacheError.GetType().Name}");
            }
            return null;
        }
        finally { NetworkGate.Release(); }
    }

    async Task<string?> ResolveResourceAsync(AudioArtworkIdentity identity, CancellationToken token)
    {
        if (Guid.TryParse(identity.ReleaseId, out Guid release))
            return "release/" + release.ToString("D");
        if (Guid.TryParse(identity.ReleaseGroupId, out Guid group))
            return "release-group/" + group.ToString("D");
        string query = "release:\"" + EscapeQuery(identity.Album!) + "\" AND artist:\"" + EscapeQuery(identity.Artist!) + "\"";
        byte[]? json = await GetAsync(new(_musicBrainz, "release?fmt=json&limit=5&query=" + Uri.EscapeDataString(query)),
            512 * 1024, token, allowRedirects: false).ConfigureAwait(false);
        return json == null ? null : SelectRelease(json, identity);
    }

    public static string? SelectRelease(byte[] json, AudioArtworkIdentity identity) =>
        MusicBrainzReleaseSearch.SelectRelease(json, identity);

    async Task<byte[]?> GetAsync(Uri uri, int limit, CancellationToken token, bool allowRedirects = true)
    {
        for (int redirects = 0; redirects <= 4; redirects++)
        {
            if (!_allowUri(uri) && (allowRedirects || !ArtistArtworkLookup.IsServiceUri(uri)))
                throw new InvalidDataException("Unexpected artwork destination.");
            DateTimeOffset retryAfter = _retryAfter.GetValueOrDefault(uri.IdnHost);
            TimeSpan delay = (_nextRequest > retryAfter ? _nextRequest : retryAfter) - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay < TimeSpan.FromDays(1) ? delay : TimeSpan.FromDays(1), token).ConfigureAwait(false);
            _nextRequest = DateTimeOffset.UtcNow + _spacing;
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            if (status is 301 or 302 or 303 or 307 or 308)
            {
                if (!allowRedirects)
                    throw new InvalidDataException("Unexpected redirect for artwork metadata or artist photo.");
                uri = response.Headers.Location is { } location ? new Uri(uri, location) : throw new InvalidDataException("Missing artwork redirect.");
                continue;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (status is 429 or 503)
            {
                TimeSpan cooldown = response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
                _retryAfter[uri.IdnHost] = DateTimeOffset.UtcNow + (cooldown > TimeSpan.FromSeconds(1) ? cooldown : TimeSpan.FromSeconds(1));
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > limit)
                throw new InvalidDataException("Artwork response exceeds the limit.");
            using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + read > limit)
                    throw new InvalidDataException("Artwork response exceeds the limit.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
        throw new InvalidDataException("Too many artwork redirects.");
    }

    internal static string EscapeQuery(string value) => string.Concat(value.Select(character =>
        "+-!(){}[]^\"~*?:\\/|&".Contains(character) ? "\\" + character : character.ToString()));
    public void Dispose() => _client.Dispose();
}
