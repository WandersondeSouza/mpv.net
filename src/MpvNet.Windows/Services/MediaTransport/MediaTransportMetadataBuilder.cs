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
    string? Subtitle = null);

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
            Clean(input.Artist),
            Clean(input.Album),
            input.TrackNumber is > 0 ? input.TrackNumber : null,
            mediaType == MediaTransportMediaType.Video ? Clean(input.Subtitle) : null);
    }

    static string FirstSafeTitle(params string?[] candidates)
    {
        foreach (string? candidate in candidates)
        {
            string? cleanCandidate = Clean(candidate);
            if (!string.IsNullOrEmpty(cleanCandidate) && !ContainsCredentials(cleanCandidate))
            {
                if (IsUri(cleanCandidate))
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

            return uri.Host;
        }

        return Clean(Path.GetFileNameWithoutExtension(cleanValue)) ?? "";
    }

    static bool ContainsCredentials(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.UserInfo);

    static bool IsUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Scheme);

    static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string cleaned = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
