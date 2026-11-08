using System.Text.Json;

namespace MpvNet;

public partial class MainPlayer
{
    internal const ulong NetworkCacheHookUserData = 1;
    internal const ulong NetworkCachePreloadedHookUserData = 6;
    NetworkCacheSession? _networkCacheSession;
    NetworkCacheMedia _networkCacheMedia;
    readonly Dictionary<string, (string Effective, string Requested)> _automaticCacheValues = new(StringComparer.Ordinal);
    readonly HashSet<string> _overriddenCacheOptions = new(StringComparer.Ordinal);
    IReadOnlySet<string> _explicitCacheOptions = MpvOptionConfiguration.EmptyOptions;
    (bool? Live, double Duration, string Protocol) _resolverCacheMetadata;
    bool _resolverCacheMetadataRead;
    long _cacheSessionStarted;
    double _lastCacheSample = double.NegativeInfinity;
    bool _cacheSeeking;
    bool _cachePlaybackReady;
    double CacheSessionSeconds => Stopwatch.GetElapsedTime(_cacheSessionStarted).TotalSeconds;

    internal override void OnHook(MpvEventSnapshot snapshot)
    {
        if (snapshot.HookName == "on_load" && snapshot.ReplyUserData == PlaylistTitleHookUserData)
        {
            ApplyPlaylistEntryOptions();
            return;
        }

        if (snapshot.HookName == "on_preloaded" && snapshot.ReplyUserData == NetworkCachePreloadedHookUserData)
        {
            ReadResolverCacheMetadata();
            RefineNetworkCache();
            return;
        }
        if (snapshot.HookName != "on_load" || snapshot.ReplyUserData != NetworkCacheHookUserData)
            return;

        string path = GetPropertyString("path");
        if (!App.AutomaticNetworkCache || !MediaInputClassifier.Classify(path).IsNetwork)
            return;
        _explicitCacheOptions = MpvOptionConfiguration.GetExplicitOptions();
        _networkCacheSession = new();
        _cacheSessionStarted = Stopwatch.GetTimestamp();
        ReadResolverCacheMetadata();
        RefineNetworkCache();
        LogNetworkCacheState("on_load");
    }

    void ReadResolverCacheMetadata()
    {
        if (_networkCacheSession is null || _resolverCacheMetadataRead ||
            !TryGetPropertyString("user-data/mpv/ytdl/json-subprocess-result/stdout", out string json))
            return;
        _resolverCacheMetadataRead = true;
        if (json.Length > 4 * 1024 * 1024)
            return;
        _resolverCacheMetadata = NetworkCacheMedia.ReadResolverMetadata(
            StreamingFailureDiagnostics.DecodeDiagnosticString(json));
    }

    string CacheProperty(string name) => TryGetPropertyString(name, out string value) ? value : "";

    void RefineNetworkCache()
    {
        if (_networkCacheSession is null || !App.AutomaticNetworkCache)
            return;
        double duration = GetPropertyDouble("duration", false);
        _networkCacheMedia = NetworkCacheMedia.Identify(GetPropertyString("path"),
            CacheProperty("stream-open-filename"), CacheProperty("file-format"),
            duration > 0 ? duration : _resolverCacheMetadata.Duration,
            GetPropertyBool("seekable", false), _resolverCacheMetadata.Live,
            _resolverCacheMetadata.Protocol, GetPropertyBool("demuxer-cache-state/eof", false));
        var resolution = NetworkCachePolicy.Resolve(_networkCacheMedia, _explicitCacheOptions);
        // With disk cache, byte caps apply to packet metadata, not disk usage.
        // Leave an explicitly enabled disk cache entirely under mpv's control.
        if (GetPropertyBool("options/cache-on-disk", false))
            return;
        foreach (StringPair option in NetworkCachePolicy.GetMissingFileOptions(resolution, name => !CanSetAutomaticCacheOption(name)))
        {
            // Refine at load boundaries only; adaptation adjusts only wait below.
            SetAutomaticCacheOption(option.Name, option.Value);
        }
    }

    bool CanSetAutomaticCacheOption(string name)
    {
        // mpv uses the larger of these two time options. Setting either one
        // beside an explicit smaller counterpart would silently defeat that cap.
        string? counterpart = name switch
        {
            "cache-secs" => "demuxer-readahead-secs",
            "demuxer-readahead-secs" => "cache-secs",
            _ => null
        };
        return (counterpart is null || OwnsOrCanSetCacheOption(counterpart)) && OwnsOrCanSetCacheOption(name);
    }

    bool OwnsOrCanSetCacheOption(string name)
    {
        if (_overriddenCacheOptions.Contains(name) || MpvOptionConfiguration.IsExplicit(_explicitCacheOptions, name))
            return false;
        if (!TryGetPropertyString("options/" + name, out string current))
            return false;
        if (_automaticCacheValues.TryGetValue(name, out var owned))
        {
            if (current == owned.Effective)
                return true;
            _overriddenCacheOptions.Add(name);
            _automaticCacheValues.Remove(name);
            return false;
        }
        // set-locally covers per-file scripts/profiles; default-value additionally
        // protects global script writes and included config that we did not parse.
        return TryGetPropertyString($"option-info/{name}/set-locally", out string local) && local == "no" &&
            TryGetPropertyString($"option-info/{name}/set-from-commandline", out string cli) && cli == "no" &&
            TryGetPropertyString($"option-info/{name}/default-value", out string baseline) && current == baseline;
    }

    void SetAutomaticCacheOption(string name, string value)
    {
        if (_automaticCacheValues.TryGetValue(name, out var owned) && owned.Requested == value)
            return;
        SetPropertyString("file-local-options/" + name, value);
        if (TryGetPropertyString("options/" + name, out string effective))
            _automaticCacheValues[name] = (effective, value);
    }

    void ResetNetworkCacheSession()
    {
        _networkCacheSession = null;
        _networkCacheMedia = default;
        _automaticCacheValues.Clear();
        _overriddenCacheOptions.Clear();
        _explicitCacheOptions = MpvOptionConfiguration.EmptyOptions;
        _resolverCacheMetadata = default;
        _resolverCacheMetadataRead = false;
        _lastCacheSample = double.NegativeInfinity;
        _cacheSeeking = false;
        _cachePlaybackReady = false;
    }

    void EndNetworkCacheSession()
    {
        _networkCacheSession?.Buffering(false, CacheSessionSeconds, false);
        LogNetworkCacheState("end-file");
        ResetNetworkCacheSession();
    }

    void OnCacheBufferingChanged(bool buffering)
    {
        _networkCacheSession?.Buffering(buffering, CacheSessionSeconds,
            _cachePlaybackReady && !_cacheSeeking && !GetPropertyBool("pause", false));
        if (_networkCacheSession is not null)
            LogNetworkCacheState(buffering ? "buffering-start" : "buffering-end");
    }

    protected override void OnSeek()
    {
        _cacheSeeking = true;
        _networkCacheSession?.Buffering(false, CacheSessionSeconds, false);
        base.OnSeek();
    }

    protected override void OnPlaybackRestart()
    {
        _cacheSeeking = false;
        _cachePlaybackReady = true;
        base.OnPlaybackRestart();
    }

    void AdaptNetworkCache()
    {
        if (_networkCacheSession is null || !App.AutomaticNetworkCache || !_cachePlaybackReady)
            return;
        double now = CacheSessionSeconds;
        if (now - _lastCacheSample < 5)
            return;
        _lastCacheSample = now;
        double audio = GetPropertyDouble("audio-bitrate", false);
        double video = GetPropertyDouble("video-bitrate", false);
        bool hasAudio = GetPropertyDouble("current-tracks/audio/id", false) > 0;
        bool hasVideo = GetPropertyDouble("current-tracks/video/id", false) > 0;
        bool missingBitrate = hasAudio && audio <= 0 || hasVideo && video <= 0;
        double bitrate = missingBitrate ? 0 : audio + video;
        var durations = ReadStreamCacheDurations(CacheProperty("demuxer-cache-state"));
        double buffered = hasAudio && !durations.Audio.HasValue || hasVideo && !durations.Video.HasValue
            ? double.NaN : Math.Min(durations.Audio ?? double.PositiveInfinity, durations.Video ?? double.PositiveInfinity);
        var sample = new NetworkCacheSample(now, GetPropertyDouble("cache-speed", false), bitrate,
            buffered, GetPropertyBool("pause", false),
            _cacheSeeking, GetPropertyBool("paused-for-cache", false));
        if (!GetPropertyBool("options/cache-pause", false) || GetPropertyBool("options/cache-on-disk", false) ||
            !CanSetAutomaticCacheOption("cache-pause-wait"))
            return;
        double? wait = _networkCacheSession.Decide(_networkCacheMedia, sample,
            GetPropertyDouble("options/cache-pause-wait", false));
        if (wait.HasValue)
        {
            SetAutomaticCacheOption("cache-pause-wait", wait.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            LogNetworkCacheState("adapted");
        }
    }

    void LogNetworkCacheState(string stage)
    {
        if (!Log.DiagnosticFileLoggingEnabled || !App.DebugMode ||
            !MediaInputClassifier.Classify(GetPropertyString("path")).IsNetwork)
            return;

        // Only fixed option names and numeric/bool values are logged. Never
        // serialize a resolved URL or the extractor's signed format metadata.
        string options = string.Join(",", new[]
        {
            "cache", "cache-on-disk", "cache-pause", "cache-pause-initial",
            "cache-pause-wait", "demuxer-max-bytes", "demuxer-max-back-bytes"
        }.Select(name => name + "=" + GetPropertyString("options/" + name)));
        Log.Debug($"Network cache state. stage={stage}, policy={(App.AutomaticNetworkCache ? "automatic" : "off")}, kind={_networkCacheMedia.Kind}, vod={_networkCacheMedia.IsVod}, live={_networkCacheMedia.IsLive}, playlistPos={GetPropertyInt("playlist-pos")}, options='{options}', bufferingCount={_networkCacheSession?.BufferingCount ?? 0}, bufferingSeconds={_networkCacheSession?.BufferingSeconds ?? 0:F3}, capacityEstimate={_networkCacheSession?.CapacityEstimate ?? "unknown"}");

        if (stage != "on_load")
        {
            Log.Debug($"Network cache buffering. position={GetPropertyDouble("time-pos", false):F3}, duration={GetPropertyDouble("demuxer-cache-duration", false):F3}, percent={GetPropertyDouble("cache-buffering-state", false):F1}, inputBytesPerSecond={GetPropertyDouble("cache-speed", false):F0}");
            string streams = FormatStreamCacheDurations(CacheProperty("demuxer-cache-state"));
            if (streams.Length > 0)
                Log.Debug("Network cache stream durations. " + streams);
        }
    }

    internal static string FormatStreamCacheDurations(string json)
    {
        var durations = ReadStreamCacheDurations(json);
        List<string> values = [];
        if (durations.Audio is double audio)
            values.Add("audio=" + audio.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (durations.Video is double video)
            values.Add("video=" + video.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(',', values);
    }

    internal static (double? Audio, double? Video) ReadStreamCacheDurations(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ts-per-stream", out JsonElement streams) ||
                streams.ValueKind != JsonValueKind.Array)
                return default;

            double? audio = null, video = null;
            foreach (JsonElement stream in streams.EnumerateArray())
            {
                if (stream.ValueKind != JsonValueKind.Object ||
                    !stream.TryGetProperty("type", out JsonElement type) || type.ValueKind != JsonValueKind.String ||
                    !stream.TryGetProperty("cache-duration", out JsonElement duration) ||
                    !duration.TryGetDoubleSafe(out double seconds) || seconds < 0)
                    continue;
                if (type.GetString() == "audio") audio = seconds;
                if (type.GetString() == "video") video = seconds;
            }
            return (audio, video);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
