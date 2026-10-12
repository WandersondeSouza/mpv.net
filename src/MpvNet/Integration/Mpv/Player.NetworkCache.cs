using System.Text.Json;

namespace MpvNet;

public partial class MainPlayer
{
    internal const ulong NetworkCacheHookUserData = 1;
    long? _bufferingStartedAt;

    internal override void OnHook(MpvEventSnapshot snapshot)
    {
        if (snapshot.HookName != "on_load" || snapshot.ReplyUserData != NetworkCacheHookUserData)
            return;

        // Keep the original page URL: yt-dlp may already have replaced the
        // stream-open-filename with an EDL, manifest, or signed media URL.
        string path = GetPropertyString("path");
        NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(path);
        foreach (StringPair option in NetworkCachePolicy.GetMissingFileOptions(resolution, name =>
            GetPropertyString($"option-info/{name}/set-locally") == "yes" ||
            GetPropertyString($"option-info/{name}/set-from-commandline") == "yes"))
        {
            SetPropertyString("file-local-options/" + option.Name, option.Value);
        }

        if (resolution.IsEnabled)
            LogNetworkCacheState("on_load", resolution.Profile);
    }

    void OnCacheBufferingChanged(bool buffering)
    {
        if (buffering)
        {
            if (_bufferingStartedAt.HasValue)
                return;

            _bufferingStartedAt = Stopwatch.GetTimestamp();
            LogNetworkCacheState("buffering-start");
        }
        else if (_bufferingStartedAt is long started)
        {
            _bufferingStartedAt = null;
            LogNetworkCacheState($"buffering-end elapsedMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}");
        }
    }

    void LogNetworkCacheState(string stage, string? profile = null)
    {
        if (!App.DebugMode || !MediaInputClassifier.Classify(GetPropertyString("path")).IsNetwork)
            return;

        // Only fixed option names and numeric/bool values are logged. Never
        // serialize a resolved URL or the extractor's signed format metadata.
        string options = string.Join(",", new[]
        {
            "cache", "cache-on-disk", "cache-pause", "cache-pause-initial",
            "cache-pause-wait", "demuxer-max-bytes", "demuxer-max-back-bytes"
        }.Select(name => name + "=" + GetPropertyString("options/" + name)));
        Log.Debug($"Network cache state. stage={stage}, profile={profile ?? App.NetworkCacheProfile}, playlistPos={GetPropertyInt("playlist-pos")}, options='{options}'");

        if (stage != "on_load")
        {
            Log.Debug($"Network cache buffering. position={GetPropertyDouble("time-pos", false):F3}, duration={GetPropertyDouble("demuxer-cache-duration", false):F3}, percent={GetPropertyDouble("cache-buffering-state", false):F1}, inputBytesPerSecond={GetPropertyDouble("cache-speed", false):F0}");
            string streams = FormatStreamCacheDurations(GetPropertyString("demuxer-cache-state"));
            if (streams.Length > 0)
                Log.Debug("Network cache stream durations. " + streams);
        }
    }

    internal static string FormatStreamCacheDurations(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "";

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("ts-per-stream", out JsonElement streams) ||
                streams.ValueKind != JsonValueKind.Array)
                return "";

            return string.Join(",", streams.EnumerateArray()
                .Where(stream => stream.ValueKind == JsonValueKind.Object &&
                    stream.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String &&
                    type.GetString() is "audio" or "video" &&
                    stream.TryGetProperty("cache-duration", out JsonElement duration) &&
                    duration.ValueKind == JsonValueKind.Number)
                .Select(stream => stream.GetProperty("type").GetString() + "=" +
                    stream.GetProperty("cache-duration").GetRawText()));
        }
        catch (JsonException)
        {
            return "";
        }
    }
}
