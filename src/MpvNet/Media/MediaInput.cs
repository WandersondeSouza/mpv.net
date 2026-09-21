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
    OnlineResolver,
    GenericNetwork
}

public sealed record MediaLoadRequest(
    string Input,
    MediaInputSource Source,
    bool Append,
    string? Title = null);

public static class MediaInputNormalizer
{
    public static MediaLoadRequest? Normalize(
        string? input,
        MediaInputSource source = MediaInputSource.Unknown,
        bool append = false,
        string? title = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string value = RemoveExternalQuotes(input.Trim());
        if (value.Length == 0 || value.StartsWith("--", StringComparison.Ordinal) ||
            value.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            return null;
        }

        if (source == MediaInputSource.RecentFiles &&
            TrySplitLegacyRecentEntry(value, out string media, out string? recentTitle))
        {
            value = media;
            title ??= recentTitle;
        }

        MediaInputClassification classification = MediaInputClassifier.Classify(value);
        if (!classification.IsValid)
            return null;

        return new MediaLoadRequest(value, source, append, title);
    }

    public static IReadOnlyList<MediaLoadRequest> NormalizeMany(
        IEnumerable<string>? inputs,
        MediaInputSource source,
        bool append = false) =>
        inputs is null
            ? []
            : inputs.Select(input => Normalize(input, source, append))
                .OfType<MediaLoadRequest>()
                .ToArray();

    static string RemoveExternalQuotes(string value) =>
        value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"'
            ? value[1..^1].Trim()
            : value;

    static bool TrySplitLegacyRecentEntry(string value, out string media, out string? title)
    {
        media = value;
        title = null;
        int separator = value.IndexOf('|');
        if (separator <= 0)
            return false;

        string candidate = value[..separator].Trim();
        if (!MediaInputClassifier.Classify(candidate).IsValid)
            return false;

        media = candidate;
        title = value[(separator + 1)..].Trim();
        return true;
    }
}

public readonly record struct MediaInputClassification(
    bool IsValid,
    bool IsNetwork,
    NetworkMediaKind NetworkKind,
    string Scheme);

public static class MediaInputClassifier
{
    static readonly HashSet<string> DirectHttpMediaExtensions = FileTypes.DefaultVideoExts
        .Concat(FileTypes.DefaultAudioExts)
        .Concat(FileTypes.DefaultImageExts)
        .Concat(FileTypes.Playlist)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
            "http" or "https" when !HasDirectHttpMediaExtension(path) => NetworkMediaKind.OnlineResolver,
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

    static bool HasDirectHttpMediaExtension(string path)
    {
        string extension = Path.GetExtension(path).TrimStart('.');
        return extension.Length > 0 && DirectHttpMediaExtensions.Contains(extension);
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

            if (string.IsNullOrWhiteSpace(value) || !IsSafeMediaInput(value))
                continue;

            MediaLoadRequest? request = MediaInputNormalizer.Normalize(value, MediaInputSource.Clipboard, append);
            if (request is not null)
                result.Add(request);
        }

        return result;
    }

    public static IReadOnlyList<MediaLoadRequest> ParseFileDropList(IEnumerable<string>? files, bool append = false) =>
        MediaInputNormalizer.NormalizeMany(files, MediaInputSource.Clipboard, append);

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

public static class MpvOptionConfiguration
{
    internal static IReadOnlySet<string> EmptyOptions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static HashSet<string> GetExplicitOptions()
    {
        IEnumerable<string> confLines = [];
        if (File.Exists(Player.ConfPath))
        {
            try
            {
                confLines = File.ReadAllLines(Player.ConfPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Debug($"Could not inspect explicit mpv options. path='{Log.SafeValue(Player.ConfPath)}', error='{Log.SafeValue(ex.Message)}'");
                return new(StringComparer.OrdinalIgnoreCase) { "*" };
            }
        }

        return ParseExplicitOptions(CommandLine.Arguments, confLines);
    }

    internal static HashSet<string> ParseExplicitOptions(
        IEnumerable<StringPair> commandLineArguments,
        IEnumerable<string> confLines)
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (StringPair pair in commandLineArguments)
            result.Add(pair.Name);

        foreach (string rawLine in confLines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int equals = line.IndexOf('=');
            string optionName = (equals > 0 ? line[..equals] : line).Trim().TrimStart('-');
            if (optionName.StartsWith("no-", StringComparison.OrdinalIgnoreCase))
                optionName = optionName[3..];
            if (optionName.Length > 0)
                result.Add(optionName);
        }

        return result;
    }

    internal static bool IsExplicit(IReadOnlySet<string> options, string name) =>
        options.Contains("*") || options.Contains(name);
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
        IReadOnlySet<string> explicitOptions = classification.IsNetwork
            ? MpvOptionConfiguration.GetExplicitOptions()
            : MpvOptionConfiguration.EmptyOptions;
        return Resolve(classification, explicitOptions);
    }

    internal static NetworkCacheResolution Resolve(string input, IReadOnlySet<string> explicitOptions)
        => Resolve(MediaInputClassifier.Classify(input), explicitOptions);

    internal static NetworkCacheResolution Resolve(
        MediaInputClassification classification,
        IReadOnlySet<string> explicitOptions)
    {
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
            NetworkMediaKind.OnlineResolver => "cache=yes,cache-pause-initial=no,demuxer-max-bytes=64MiB",
            _ => "cache=yes,cache-pause-initial=no,demuxer-max-bytes=64MiB"
        };

        return new(classification.NetworkKind, profile, RemoveExplicitOptions(options, explicitOptions));
    }

    public static string NormalizeProfile(string? profile) =>
        profile?.Trim().ToLowerInvariant() is "off" or "low-latency" or "balanced" or "resilient"
            ? profile.Trim().ToLowerInvariant()
            : "balanced";

    static string RemoveExplicitOptions(string options, IReadOnlySet<string> explicitOptions) =>
        string.Join(',', options.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(option =>
            {
                int equals = option.IndexOf('=');
                string name = equals > 0 ? option[..equals] : option;
                return !NetworkOptions.Contains(name) || !MpvOptionConfiguration.IsExplicit(explicitOptions, name);
            }));
}

public static class MediaIpcMessage
{
    public static string Serialize(string mode, IEnumerable<string> arguments)
    {
        if (!IsSupportedMode(mode))
            throw new ArgumentException("Unsupported media IPC mode.", nameof(mode));

        return JsonSerializer.Serialize(new Payload(1, mode, arguments.ToArray()));
    }

    public static bool TryParse(string? value, out string mode, out string[] arguments)
    {
        mode = "";
        arguments = [];
        if (string.IsNullOrEmpty(value))
            return false;

        try
        {
            Payload? payload = JsonSerializer.Deserialize<Payload>(value);
            if (payload?.Version == 1 && IsSupportedMode(payload.Mode))
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
        return IsSupportedMode(mode);
    }

    static bool IsSupportedMode(string? mode) => mode is "single" or "queue" or "command";

    sealed record Payload(int Version, string Mode, string[]? Arguments);
}
