using System.IO;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed record MediaTransportMetadataInput(
    string? Path,
    string? MediaTitle,
    string? FileName,
    bool HasVideo,
    bool HasAudio,
    string? Artist = null,
    string? Album = null,
    uint? TrackNumber = null,
    string? Subtitle = null,
    string? AlbumArtist = null,
    string? ThumbnailPath = null);

public static class MediaTransportMetadataBuilder
{
    public static MediaTransportMetadata Build(MediaTransportMetadataInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        MediaTransportMediaType mediaType = input.HasVideo
            ? MediaTransportMediaType.Video
            : input.HasAudio
                ? MediaTransportMediaType.Music
                : MediaTransportMediaType.Unknown;

        string title = FirstSafeTitle(input.MediaTitle, input.FileName, input.Path);

        return new MediaTransportMetadata(
            title,
            mediaType,
            CleanTag(input.Artist),
            CleanTag(input.Album),
            input.TrackNumber is > 0 ? input.TrackNumber : null,
            mediaType == MediaTransportMediaType.Video ? CleanTag(input.Subtitle) : null,
            ThumbnailPath: GetLocalMediaPath(input.ThumbnailPath),
            AlbumArtist: mediaType == MediaTransportMediaType.Music ? CleanTag(input.AlbumArtist) : null,
            LocalMediaPath: GetLocalMediaPath(input.Path));
    }

    static string FirstSafeTitle(params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            string? cleanCandidate = Clean(candidate);
            if (!string.IsNullOrEmpty(cleanCandidate) && !ContainsCredentials(cleanCandidate))
            {
                if (IsUri(cleanCandidate) || LooksLikePathOrUrl(cleanCandidate))
                    continue;

                return cleanCandidate;
            }
        }

        string pathLabel = GetSafePathLabel(candidates.LastOrDefault());
        return string.IsNullOrEmpty(pathLabel) ? "Untitled" : pathLabel;
    }

    static string GetSafePathLabel(string? value)
    {
        string? cleanValue = Clean(value);
        if (string.IsNullOrEmpty(cleanValue))
            return "";

        if (Uri.TryCreate(cleanValue, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host))
        {
            string segment = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? "";

            if (!string.IsNullOrEmpty(segment))
                return Clean(Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(segment))) ?? "";

            return "Untitled";
        }

        return Clean(Path.GetFileNameWithoutExtension(cleanValue)) ?? "";
    }

    static bool ContainsCredentials(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.UserInfo);

    static bool IsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Scheme);

    // Internal thumbnail source only; never assigned to a Windows display property.
    public static string? GetLocalMediaPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Length > 3 && char.IsAsciiLetter(path[0]) &&
        path[1] == ':' && path[2] is '\\' or '/' && !path.Contains("://", StringComparison.Ordinal)
            ? path : null;

    static bool LooksLikePathOrUrl(string value) =>
        value.Contains("://", StringComparison.Ordinal) || value.Contains('\\') || value.StartsWith('/');

    static string? CleanTag(string? value)
    {
        string? cleaned = Clean(value);
        return cleaned != null && !IsUri(cleaned) && !LooksLikePathOrUrl(cleaned) ? cleaned : null;
    }

    static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
