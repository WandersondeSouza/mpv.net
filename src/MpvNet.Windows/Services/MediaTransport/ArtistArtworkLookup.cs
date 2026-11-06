using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed record AudioArtistIdentity(string? Name, string? ArtistId)
{
    const string VariousArtists = "89ad4ac3-39f7-470e-963a-56509c546377";
    static readonly string[] ArtistJoiners = [" FEAT", " FT.", " FEATURING ", " WITH ", " AND ", " VS. "];

    internal string? MusicBrainzId => CanonicalId(ArtistId);
    internal string? SearchName => Name is { } name && IsArtistName(name) && !LooksComposite(name) ? name.Trim() : null;

    internal string? Key
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Name) && !IsArtistName(Name))
                return null;
            if (MusicBrainzId is { } id)
            {
                // A composite credit must match the canonical entity name. A
                // failed match must not invalidate art cached for the MBID alone.
                string suffix = Name is { } tag && LooksComposite(tag) ? "\n" + AudioArtworkIdentity.Normalize(tag) : "";
                return "artist:mbid:" + id + suffix;
            }
            return string.IsNullOrWhiteSpace(ArtistId) && SearchName is { } name
                ? "artist:tags:" + AudioArtworkIdentity.Normalize(name) : null;
        }
    }

    internal bool Matches(ArtistArtworkSource source) => source.IsVerified &&
        (MusicBrainzId is not { } id || id == source.MusicBrainzId) && MatchesName(source.ArtistName);

    internal bool MatchesName(string? artistName)
    {
        if (!IsArtistName(artistName))
            return false;
        if (MusicBrainzId != null)
            return Name is not { } tag || !LooksComposite(tag) || SameName(tag, artistName!);
        return SearchName is { } name && SameName(name, artistName!);
    }

    internal static string? CanonicalId(string? value) => Guid.TryParse(value, out Guid id) &&
        id != Guid.Empty && id.ToString("D") != VariousArtists ? id.ToString("D") : null;

    internal static bool IsArtistName(string? value) => AudioArtworkIdentity.IsTag(value) &&
        AudioArtworkIdentity.Normalize(value!) is not ("VARIOUS ARTISTS" or "VARIOUS" or "V.A." or
            "UNKNOWN ARTIST" or "UNKNOWN" or "[UNKNOWN]" or "[NO ARTIST]") &&
        !value!.Contains(';') && !value.Contains('|');

    internal static bool SameName(string left, string right) =>
        AudioArtworkIdentity.Normalize(left) == AudioArtworkIdentity.Normalize(right);

    static bool LooksComposite(string name) => name.Any(c => "/,&+".Contains(c)) ||
        ArtistJoiners.Any(AudioArtworkIdentity.Normalize(name).Contains);
}

public sealed record ArtistArtworkSource(string MusicBrainzId, string ArtistName, string WikidataId,
    string FileTitle, string PageUrl, string License, string? LicenseUrl, string Author, DateTimeOffset VerifiedAt)
{
    internal bool IsVerified => AudioArtistIdentity.CanonicalId(MusicBrainzId) == MusicBrainzId &&
        MusicBrainzId != null && AudioArtistIdentity.IsArtistName(ArtistName) &&
        ArtistArtworkLookup.IsWikidataId(WikidataId) &&
        ArtistArtworkLookup.IsFilePage(PageUrl, FileTitle) && ArtistArtworkLookup.IsAuthor(Author) &&
        ArtistArtworkLookup.IsAllowedLicense(License, LicenseUrl) &&
        VerifiedAt <= DateTimeOffset.UtcNow && VerifiedAt > DateTimeOffset.UtcNow.AddDays(-30);
}

internal static class ArtistArtworkLookup
{
    const int MaxMetadataBytes = 512 * 1024;
    const string CommonsUploadPath = "/wikipedia/commons/";
    const string CommonsPagePath = "/wiki/";
    const string WikidataPath = "/wiki/";

    internal static async Task<(byte[]? Image, ArtistArtworkSource? Artist)> ResolveAsync(AudioArtistIdentity identity,
        Uri musicBrainz, Func<Uri, int, Task<byte[]?>> get)
    {
        string? mbid = identity.MusicBrainzId;
        if (mbid == null && identity.SearchName is { } name)
        {
            string query = "artist:\"" + OnlineAudioArtwork.EscapeQuery(name) + "\"";
            using var search = await ReadAsync(new(musicBrainz, "artist?fmt=json&limit=5&query=" +
                Uri.EscapeDataString(query)), get).ConfigureAwait(false);
            if (search == null)
                return default;
            JsonElement matches = search.RootElement.GetProperty("artists");
            // A truncated result cannot prove uniqueness, even with an exact first match.
            if (search.RootElement.GetProperty("count").GetInt32() != matches.GetArrayLength())
                return default;
            JsonElement[] exact = matches.EnumerateArray().Where(candidate =>
                candidate.GetProperty("name").GetString() is { } candidateName &&
                AudioArtistIdentity.SameName(candidateName, name)).ToArray();
            if (exact.Length == 1)
                mbid = AudioArtistIdentity.CanonicalId(exact[0].GetProperty("id").GetString());
        }
        if (mbid == null)
            return default;

        using var artist = await ReadAsync(new(musicBrainz, $"artist/{mbid}?fmt=json&inc=url-rels"), get).ConfigureAwait(false);
        if (artist == null || AudioArtistIdentity.CanonicalId(artist.RootElement.GetProperty("id").GetString()) != mbid)
            return default;
        string? artistName = artist.RootElement.GetProperty("name").GetString();
        if (!identity.MatchesName(artistName))
            return default;

        JsonElement[] links = artist.RootElement.GetProperty("relations").EnumerateArray().Where(relation =>
            relation.GetProperty("type").GetString() == "wikidata" &&
            relation.GetProperty("ended").ValueKind == JsonValueKind.False).ToArray();
        if (links.Length != 1 || !TryServiceUri(links[0].GetProperty("url").GetProperty("resource").GetString(),
                "www.wikidata.org", out Uri? link) || !link!.AbsolutePath.StartsWith(WikidataPath, StringComparison.Ordinal))
            return default;
        string wikidataId = link.AbsolutePath[WikidataPath.Length..];
        if (!IsWikidataId(wikidataId))
            return default;

        using var wikidata = await ReadAsync(new("https://www.wikidata.org/w/api.php?action=wbgetentities&props=claims&format=json&ids=" +
            wikidataId), get).ConfigureAwait(false);
        if (wikidata == null)
            return default;
        JsonElement entity = wikidata.RootElement.GetProperty("entities").GetProperty(wikidataId);
        if (entity.GetProperty("id").GetString() != wikidataId)
            return default;
        JsonElement claims = entity.GetProperty("claims");
        if (AudioArtistIdentity.CanonicalId(SingleClaim(claims, "P434")) != mbid ||
            SingleClaim(claims, "P18") is not { } fileName || !IsFileName(fileName))
            return default;

        string title = "File:" + fileName;
        using var commons = await ReadAsync(new("https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2" +
            "&prop=imageinfo&iilocalonly=1&iiprop=url%7Ccanonicaltitle%7Csize%7Cmime%7Cextmetadata&iiurlwidth=500&iiurlheight=500" +
            "&iiextmetadatalanguage=en&iiextmetadatafilter=Artist%7CLicenseShortName%7CLicenseUrl%7CCopyrighted%7CAttributionRequired" +
            "&titles=" + Uri.EscapeDataString(title)), get).ConfigureAwait(false);
        if (commons == null)
            return default;
        JsonElement pages = commons.RootElement.GetProperty("query").GetProperty("pages");
        if (pages.GetArrayLength() != 1)
            return default;
        JsonElement images = pages[0].GetProperty("imageinfo");
        if (images.GetArrayLength() != 1)
            return default;
        JsonElement info = images[0];
        if (!SameTitle(title, info.GetProperty("canonicaltitle").GetString()) ||
            info.GetProperty("mime").GetString() is not ("image/jpeg" or "image/png") ||
            GetImageUri(info, fileName) is not { } imageUri)
            return default;

        JsonElement metadata = info.GetProperty("extmetadata");
        string? author = Metadata(metadata, "Artist");
        string? licenseUrl = Metadata(metadata, "LicenseUrl");
        string? license = Metadata(metadata, "LicenseShortName")?.Trim().ToUpperInvariant() switch
        {
            "PUBLIC DOMAIN" => "PD",
            "CC0" => "CC0",
            _ => null
        };
        // Commons template metadata can be HTML or contain multiple values.
        // Accept exact, consistent rights signals only.
        if (license == null || !string.Equals(Metadata(metadata, "Copyrighted"), "false", StringComparison.OrdinalIgnoreCase) ||
            Metadata(metadata, "AttributionRequired") is { } attribution &&
                !string.Equals(attribution, "false", StringComparison.OrdinalIgnoreCase))
            return default;
        var source = new ArtistArtworkSource(mbid, artistName!, wikidataId, title,
            info.GetProperty("descriptionurl").GetString() ?? "", license, licenseUrl, author ?? "", DateTimeOffset.UtcNow);
        if (!identity.Matches(source))
            return default;
        byte[]? image = await get(imageUri, AudioArtworkImage.MaxBytes).ConfigureAwait(false);
        return (image, source);
    }

    static async Task<JsonDocument?> ReadAsync(Uri uri, Func<Uri, int, Task<byte[]?>> get) =>
        await get(uri, MaxMetadataBytes).ConfigureAwait(false) is { } bytes ? JsonDocument.Parse(bytes) : null;

    static string? SingleClaim(JsonElement claims, string property)
    {
        if (!claims.TryGetProperty(property, out JsonElement values))
            return null;
        JsonElement[] active = values.EnumerateArray().Where(value => value.GetProperty("rank").GetString() != "deprecated").ToArray();
        if (active.Length != 1 || active[0].GetProperty("rank").GetString() is not ("normal" or "preferred"))
            return null;
        JsonElement snak = active[0].GetProperty("mainsnak");
        return snak.GetProperty("snaktype").GetString() == "value"
            ? snak.GetProperty("datavalue").GetProperty("value").GetString() : null;
    }

    static string? Metadata(JsonElement metadata, string field) =>
        metadata.TryGetProperty(field, out JsonElement item) ? item.GetProperty("value").GetString() : null;

    static Uri? GetImageUri(JsonElement info, string fileName)
    {
        if (!TryServiceUri(info.GetProperty("url").GetString(), "upload.wikimedia.org", out Uri? original) ||
            !original!.AbsolutePath.StartsWith(CommonsUploadPath, StringComparison.Ordinal) ||
            !SameTitle(fileName, Uri.UnescapeDataString(original.Segments[^1])))
            return null;
        if (!info.TryGetProperty("thumburl", out JsonElement thumbnail))
            return original;
        if (!TryServiceUri(thumbnail.GetString(), "upload.wikimedia.org", out Uri? thumb))
            return null;
        string thumbPrefix = CommonsUploadPath + "thumb/" + original.AbsolutePath[CommonsUploadPath.Length..] + "/";
        return thumb == original || thumb!.AbsolutePath.StartsWith(thumbPrefix, StringComparison.Ordinal) &&
            !thumb.AbsolutePath[thumbPrefix.Length..].Contains('/') ? thumb : null;
    }

    internal static bool IsWikidataId(string? id) => id is { Length: >= 2 and <= 20 } &&
        id[0] == 'Q' && id[1] is >= '1' and <= '9' && id.Skip(2).All(char.IsAsciiDigit);

    internal static bool IsFilePage(string? url, string? title) => title?.StartsWith("File:", StringComparison.Ordinal) == true &&
        IsFileName(title[5..]) && TryServiceUri(url, "commons.wikimedia.org", out Uri? page) &&
        page!.AbsolutePath.StartsWith(CommonsPagePath, StringComparison.Ordinal) &&
        SameTitle(title, Uri.UnescapeDataString(page.AbsolutePath[CommonsPagePath.Length..]));

    static bool IsFileName(string name) => name.Length is > 0 and <= 255 &&
        !name.Any(c => c is '/' or '\\' or ':' or '|' or '#' or '?' or '[' or ']' || char.IsControl(c));

    static bool SameTitle(string expected, string? actual) => expected.Replace('_', ' ') == actual?.Replace('_', ' ');

    internal static bool IsAuthor(string? author)
    {
        if (string.IsNullOrWhiteSpace(author) || author.Length > 1024)
            return false;
        string name = WebUtility.HtmlDecode(Regex.Replace(author, "<[^>]*>", "", RegexOptions.NonBacktracking))
            .Trim(' ', '\t', '\r', '\n', '[', ']', '(', ')');
        return name.Any(char.IsLetter) && !name.StartsWith("unknown", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("anonymous", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsAllowedLicense(string license, string? url) => license switch
    {
        "CC0" => url is "https://creativecommons.org/publicdomain/zero/1.0/" or
            "http://creativecommons.org/publicdomain/zero/1.0/",
        "PD" => url is null or "" or "https://commons.wikimedia.org/wiki/Help:Public_domain" or
            "https://creativecommons.org/publicdomain/mark/1.0/" or "http://creativecommons.org/publicdomain/mark/1.0/",
        _ => false
    };

    internal static bool IsServiceUri(Uri uri) => IsHttps(uri) &&
        (uri.IdnHost is "www.wikidata.org" or "commons.wikimedia.org" && uri.AbsolutePath == "/w/api.php" ||
         uri.IdnHost == "upload.wikimedia.org" && uri.AbsolutePath.StartsWith(CommonsUploadPath, StringComparison.Ordinal) &&
            uri.Query.Length == 0);

    static bool TryServiceUri(string? value, string host, out Uri? uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri) && IsHttps(uri) && uri.IdnHost == host && uri.Query.Length == 0;

    static bool IsHttps(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort &&
        uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
