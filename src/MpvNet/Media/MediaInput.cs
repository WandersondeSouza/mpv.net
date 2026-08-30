using System.Text.Json;

namespace MpvNet;

public enum MediaInputSource
{
    CommandLine,
    Clipboard,
    DragAndDrop,
    FileDialog,
    Playlist,
    RecentFiles,
    InterProcessMessage,
    InternalCommand,
    Unknown
}

public enum NetworkMediaKind
{
    None,
    HttpProgressive,
    Hls,
    Dash,
    FtpFile,
    SftpFile,
    Rtsp,
    Rtmp,
    DatagramLive,
    GenericNetwork
}

public sealed record MediaLoadRequest(
    string Input,
    MediaInputSource Source,
    bool Append,
    string? Title = null);

public readonly record struct MediaInputClassification(
    bool IsValid,
    bool IsNetwork,
    NetworkMediaKind NetworkKind,
    string Scheme);

public static class MediaInputClassifier
{
    public static MediaInputClassification Classify(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input == "-")
            return new(true, false, NetworkMediaKind.None, "");

        string value = input.Trim();

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            string.IsNullOrWhiteSpace(uri.Scheme))
        {
            return new(File.Exists(value) || !value.Contains("://"), false, NetworkMediaKind.None, "");
        }

        string scheme = uri.Scheme.ToLowerInvariant();
        if (!FileTypes.IsNetworkScheme(scheme) || string.IsNullOrWhiteSpace(uri.Host))
            return new(!value.Contains("://") || scheme == "file", false, NetworkMediaKind.None, scheme);

        string path = uri.AbsolutePath;
        NetworkMediaKind kind = scheme switch
        {
            "http" or "https" when path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) => NetworkMediaKind.Hls,
            "http" or "https" when path.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase) => NetworkMediaKind.Dash,
            "http" or "https" => NetworkMediaKind.HttpProgressive,
            "ftp" or "ftps" => NetworkMediaKind.FtpFile,
            "sftp" => NetworkMediaKind.SftpFile,
            "rtsp" => NetworkMediaKind.Rtsp,
            "rtmp" or "rtmps" => NetworkMediaKind.Rtmp,
            "udp" or "tcp" or "srt" or "srtp" => NetworkMediaKind.DatagramLive,
            _ => NetworkMediaKind.GenericNetwork
        };

        return new(true, true, kind, scheme);
    }
}

public static class ClipboardMediaParser
{
    public static IReadOnlyList<MediaLoadRequest> ParseText(string? text, bool append = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        List<MediaLoadRequest> result = [];
        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string value = rawLine.Trim();
            if (value.Length == 0 || value.StartsWith('#') || value.StartsWith("--", StringComparison.Ordinal))
                continue;

            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"')
                value = value[1..^1].Trim();

            if (string.IsNullOrWhiteSpace(value) || !IsSafeMediaInput(value))
                continue;

            result.Add(new MediaLoadRequest(value, MediaInputSource.Clipboard, append));
        }

        return result;
    }

    public static IReadOnlyList<MediaLoadRequest> ParseFileDropList(IEnumerable<string>? files, bool append = false) =>
        files is null
            ? []
            : files.Where(file => !string.IsNullOrWhiteSpace(file))
                .Select(file => new MediaLoadRequest(file, MediaInputSource.Clipboard, append))
                .ToArray();

    static bool IsSafeMediaInput(string value) =>
        value == "-" ||
        FileTypes.IsStreamingUrl(value) ||
        File.Exists(value) ||
        FileTypes.IsSupportedMediaInput(value);
}

public sealed record NetworkCacheResolution(NetworkMediaKind Kind, string Profile, string Options)
{
    public bool IsEnabled => !string.IsNullOrEmpty(Options);
}

public static class NetworkCachePolicy
{
    public const string BalancedHttpOptions = "cache=yes,cache-pause-initial=yes,cache-pause-wait=3,demuxer-max-bytes=128MiB";

    static readonly HashSet<string> NetworkOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "cache", "cache-on-disk", "cache-pause", "cache-pause-initial", "cache-pause-wait", "cache-secs",
        "demuxer-max-bytes", "demuxer-max-back-bytes", "demuxer-readahead-secs", "network-timeout"
    };

    public static NetworkCacheResolution Resolve(string input)
    {
        MediaInputClassification classification = MediaInputClassifier.Classify(input);
        if (!App.AutomaticNetworkCache || !classification.IsNetwork)
            return new(classification.NetworkKind, App.NetworkCacheProfile, "");

        string profile = NormalizeProfile(App.NetworkCacheProfile);
        if (profile == "off")
            return new(classification.NetworkKind, profile, "");

        string options = classification.NetworkKind switch
        {
            NetworkMediaKind.HttpProgressive or NetworkMediaKind.Dash =>
                profile == "low-latency"
                    ? "cache=yes,cache-pause-initial=no,demuxer-max-bytes=64MiB"
                    : profile == "resilient" ? "cache=yes,cache-pause-initial=yes,cache-pause-wait=5,demuxer-max-bytes=256MiB" : BalancedHttpOptions,
            NetworkMediaKind.Hls =>
                profile == "low-latency"
                    ? "cache=yes,cache-pause-initial=no,demuxer-max-bytes=32MiB"
                    : profile == "resilient" ? "cache=yes,cache-pause-initial=yes,cache-pause-wait=5,demuxer-max-bytes=128MiB" : "cache=yes,cache-pause-initial=yes,cache-pause-wait=3,demuxer-max-bytes=64MiB",
            NetworkMediaKind.FtpFile or NetworkMediaKind.SftpFile =>
                "cache=yes,cache-on-disk=yes,demuxer-max-bytes=128MiB",
            NetworkMediaKind.Rtsp or NetworkMediaKind.Rtmp or NetworkMediaKind.DatagramLive =>
                "cache=yes,cache-pause-initial=no,cache-pause-wait=1,demuxer-max-bytes=32MiB",
            _ => "cache=yes,cache-pause-initial=no,demuxer-max-bytes=64MiB"
        };

        return new(classification.NetworkKind, profile, RemoveExplicitOptions(options));
    }

    public static string NormalizeProfile(string? profile) =>
        profile?.Trim().ToLowerInvariant() is "off" or "low-latency" or "balanced" or "resilient"
            ? profile.Trim().ToLowerInvariant()
            : "balanced";

    static string RemoveExplicitOptions(string options) =>
        string.Join(',', options.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(option =>
            {
                int equals = option.IndexOf('=');
                string name = equals > 0 ? option[..equals] : option;
                return !NetworkOptions.Contains(name) || !HasExplicitOption(name);
            }));

    static bool HasExplicitOption(string name)
    {
        if (CommandLine.Arguments.Any(pair => pair.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!File.Exists(Player.ConfPath))
            return false;

        try
        {
            foreach (string rawLine in File.ReadLines(Player.ConfPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                int equals = line.IndexOf('=');
                string optionName = (equals > 0 ? line[..equals] : line).Trim().TrimStart('-');
                if (optionName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug($"Could not inspect explicit network option. path='{Log.SafeValue(Player.ConfPath)}', option='{Log.SafeValue(name)}', error='{Log.SafeValue(ex.Message)}'");
            return true;
        }

        return false;
    }
}

public static class MediaIpcMessage
{
    public static string Serialize(string mode, IEnumerable<string> arguments) =>
        JsonSerializer.Serialize(new Payload(1, mode, arguments.ToArray()));

    public static bool TryParse(string? value, out string mode, out string[] arguments)
    {
        mode = "";
        arguments = [];
        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            Payload? payload = JsonSerializer.Deserialize<Payload>(value);
            if (payload?.Version == 1 && !string.IsNullOrWhiteSpace(payload.Mode))
            {
                mode = payload.Mode;
                arguments = payload.Arguments ?? [];
                return true;
            }
        }
        catch (JsonException)
        {
        }

        string[] legacy = value.Split('\n');
        if (legacy.Length == 0 || string.IsNullOrWhiteSpace(legacy[0]))
            return false;

        mode = legacy[0];
        arguments = legacy.Skip(1).ToArray();
        return mode is "single" or "queue" or "command";
    }

    sealed record Payload(int Version, string Mode, string[]? Arguments);
}
