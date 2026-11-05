using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed record AudioArtworkIdentity(string? Artist, string? Album, string? ReleaseId = null, string? ReleaseGroupId = null)
{
    public string? Key => Guid.TryParse(ReleaseId, out Guid release) ? "release:" + release.ToString("D") :
        Guid.TryParse(ReleaseGroupId, out Guid group) ? "release-group:" + group.ToString("D") :
        IsTag(Artist) && IsTag(Album) ? "tags:" + Normalize(Artist!) + "\n" + Normalize(Album!) : null;

    public static string Normalize(string value) => value.Normalize(NormalizationForm.FormKC).Trim().ToUpperInvariant();
    static bool IsTag(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 &&
        !value.Contains("://", StringComparison.Ordinal) && !value.Contains('\\') && !value.Any(char.IsControl);
}

public sealed class OnlineAudioArtwork : IDisposable
{
    static readonly SemaphoreSlim NetworkGate = new(1);
    static DateTimeOffset _nextRequest;
    DateTimeOffset _retryAfter;
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

    public async Task<string?> FindAsync(AudioArtworkIdentity identity, CancellationToken token)
    {
        string? key = identity.Key;
        if (key == null)
            return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_timeout);
        CancellationToken requestToken = timeout.Token;
        // Serialize the cache check and request, coalescing consecutive tracks
        // from the same album. Cancellation releases the gate immediately.
        await NetworkGate.WaitAsync(requestToken).ConfigureAwait(false);
        try
        {
            var cached = await _cache.FindAsync(key, requestToken).ConfigureAwait(false);
            if (cached.Found)
                return cached.Path;
            string? resource = Guid.TryParse(identity.ReleaseId, out Guid release) ? "release/" + release.ToString("D") :
                Guid.TryParse(identity.ReleaseGroupId, out Guid group) ? "release-group/" + group.ToString("D") : null;
            if (resource == null)
            {
                string query = "release:\"" + EscapeQuery(identity.Album!) + "\" AND artist:\"" + EscapeQuery(identity.Artist!) + "\"";
                byte[]? json = await GetAsync(new(_musicBrainz, "release?fmt=json&limit=5&query=" + Uri.EscapeDataString(query)),
                    512 * 1024, requestToken).ConfigureAwait(false);
                if (json != null)
                    resource = SelectRelease(json, identity);
            }
            string? image = null;
            if (resource != null)
            {
                byte[]? bytes = await GetAsync(new(_coverArt, resource + "/front-500"), AudioArtworkImage.MaxBytes, requestToken).ConfigureAwait(false);
                if (bytes != null)
                    image = await _cache.StoreImageAsync(await AudioArtworkImage.NormalizeAsync(bytes, requestToken).ConfigureAwait(false), requestToken).ConfigureAwait(false);
            }
            await _cache.RememberAsync(key, image, image == null ? TimeSpan.FromDays(1) : TimeSpan.FromDays(30), requestToken).ConfigureAwait(false);
            return image;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            await _cache.RememberAsync(key, null, TimeSpan.FromMinutes(5), token).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Cache transient failures briefly as well; a playlist of one album
            // must not hammer an unavailable service once per track.
            await _cache.RememberAsync(key, null, TimeSpan.FromMinutes(5), token).ConfigureAwait(false);
            return null;
        }
        finally { NetworkGate.Release(); }
    }

    public static string? SelectRelease(byte[] json, AudioArtworkIdentity identity)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("releases", out JsonElement releases) || releases.ValueKind != JsonValueKind.Array)
            return null;
        List<(string Id, string Group, bool HasGroup)> matches = [];
        foreach (JsonElement release in releases.EnumerateArray())
        {
            if (!release.TryGetProperty("score", out JsonElement score) || !score.TryGetInt32(out int confidence) || confidence < 95 ||
                !release.TryGetProperty("title", out JsonElement title) || title.ValueKind != JsonValueKind.String ||
                AudioArtworkIdentity.Normalize(title.GetString()!) != AudioArtworkIdentity.Normalize(identity.Album!) ||
                !release.TryGetProperty("id", out JsonElement id) || !Guid.TryParse(id.GetString(), out Guid releaseId) ||
                !release.TryGetProperty("artist-credit", out JsonElement credits) || credits.ValueKind != JsonValueKind.Array)
                continue;
            string credited = string.Concat(credits.EnumerateArray().Select(credit =>
                (credit.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : "") +
                (credit.TryGetProperty("joinphrase", out var join) && join.ValueKind == JsonValueKind.String ? join.GetString() : "")));
            if (AudioArtworkIdentity.Normalize(credited) != AudioArtworkIdentity.Normalize(identity.Artist!))
                continue;
            Guid groupId = default;
            bool hasGroup = release.TryGetProperty("release-group", out var rg) && rg.TryGetProperty("id", out var gid) &&
                Guid.TryParse(gid.GetString(), out groupId);
            matches.Add((releaseId.ToString("D"), hasGroup ? groupId.ToString("D") : releaseId.ToString("D"), hasGroup));
        }
        // Several editions of one album are acceptable; different groups with
        // the same title/credit are ambiguous and must not be guessed.
        if (matches.Count == 0 || matches.Select(match => match.Group).Distinct().Count() != 1)
            return null;
        // Tags identify the album, not a particular pressing. CAA's group
        // endpoint selects its representative artwork; the first search hit
        // may be a perfectly matching edition with no uploaded picture.
        return matches[0].HasGroup ? "release-group/" + matches[0].Group : "release/" + matches[0].Id;
    }

    async Task<byte[]?> GetAsync(Uri uri, int limit, CancellationToken token)
    {
        for (int redirects = 0; redirects <= 4; redirects++)
        {
            if (!_allowUri(uri))
                throw new InvalidDataException("Unexpected artwork destination.");
            TimeSpan delay = (_nextRequest > _retryAfter ? _nextRequest : _retryAfter) - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, token).ConfigureAwait(false);
            _nextRequest = DateTimeOffset.UtcNow + _spacing;
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            if (status is 301 or 302 or 303 or 307 or 308)
            {
                uri = response.Headers.Location is { } location ? new Uri(uri, location) : throw new InvalidDataException("Missing artwork redirect.");
                continue;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (status is 429 or 503)
            {
                TimeSpan cooldown = response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
                _retryAfter = DateTimeOffset.UtcNow + (cooldown > TimeSpan.FromSeconds(1) ? cooldown : TimeSpan.FromSeconds(1));
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

    static string EscapeQuery(string value) => string.Concat(value.Select(character =>
        "+-!(){}[]^\"~*?:\\/|&".Contains(character) ? "\\" + character : character.ToString()));
    public void Dispose() => _client.Dispose();
}
