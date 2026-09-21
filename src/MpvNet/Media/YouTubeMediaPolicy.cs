namespace MpvNet;

public readonly record struct YouTubeUrlInfo(
    bool IsYouTube,
    bool HasVideo,
    bool HasPlaylist,
    int? RequestedIndex)
{
    public bool RequestsPlaylist => IsYouTube && HasPlaylist;
}

public static class YouTubeMediaPolicy
{
    public const string NativePlaylistLoadOption = "ytdl-raw-options-append=yes-playlist=";

    public static YouTubeUrlInfo Analyze(string? input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) || !IsYouTubeHost(uri.Host))
            return default;

        Dictionary<string, string> query = ParseQuery(uri.Query);
        bool hasVideo = uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            ? uri.AbsolutePath.Trim('/').Length > 0
            : query.TryGetValue("v", out string? video) && !string.IsNullOrWhiteSpace(video);
        bool hasPlaylist = query.TryGetValue("list", out string? playlist) && !string.IsNullOrWhiteSpace(playlist);
        int? index = query.TryGetValue("index", out string? indexText) &&
            int.TryParse(indexText, out int parsedIndex) && parsedIndex > 0
                ? parsedIndex
                : null;

        return new(true, hasVideo, hasPlaylist, index);
    }

    public static bool ShouldEnableNativePlaylist(string input) => Analyze(input).RequestsPlaylist;

    static bool IsYouTubeHost(string host) =>
        host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);

    static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        ReadOnlySpan<char> remaining = query.AsSpan().TrimStart('?');

        while (!remaining.IsEmpty)
        {
            int separator = remaining.IndexOf('&');
            ReadOnlySpan<char> pair = separator >= 0 ? remaining[..separator] : remaining;
            int equals = pair.IndexOf('=');
            ReadOnlySpan<char> key = equals >= 0 ? pair[..equals] : pair;
            ReadOnlySpan<char> value = equals >= 0 ? pair[(equals + 1)..] : [];

            if (!key.IsEmpty)
                result[key.ToString()] = value.ToString();

            if (separator < 0)
                break;

            remaining = remaining[(separator + 1)..];
        }

        return result;
    }
}
