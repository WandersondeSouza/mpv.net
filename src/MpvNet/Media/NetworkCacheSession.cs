using System.Text.Json;

namespace MpvNet;

internal readonly record struct NetworkCacheMedia(NetworkMediaKind Kind, bool IsVod, bool IsLive)
{
    internal static NetworkCacheMedia Identify(string original, string resolved, string format,
        double duration, bool seekable, bool? resolverLive = null, string? protocol = "", bool eof = false)
    {
        MediaInputClassification input = MediaInputClassifier.Classify(original);
        if (!input.IsNetwork)
            return new(NetworkMediaKind.None, false, false);
        NetworkMediaKind kind = input.NetworkKind;
        MediaInputClassification target = MediaInputClassifier.Classify(resolved);
        if (target.IsNetwork && target.NetworkKind != NetworkMediaKind.OnlineResolver)
            kind = target.NetworkKind;
        protocol ??= "";
        if (format.Split(',').Contains("hls") || protocol.Contains("m3u8", StringComparison.OrdinalIgnoreCase))
            kind = NetworkMediaKind.Hls;
        else if (format.Split(',').Contains("dash") || protocol.Contains("dash", StringComparison.OrdinalIgnoreCase))
            kind = NetworkMediaKind.Dash;

        bool live = resolverLive == true || kind is NetworkMediaKind.Rtsp or NetworkMediaKind.Rtmp or NetworkMediaKind.DatagramLive;
        bool finite = double.IsFinite(duration) && duration > 0;
        // Live manifests can expose finite DVR windows. A duration alone does
        // not prove VOD; require resolver evidence or the demuxer reaching EOF.
        bool vod = !live && (resolverLive == false || finite && seekable &&
            (kind is not (NetworkMediaKind.Hls or NetworkMediaKind.Dash) || eof));
        return new(kind, vod, live);
    }

    internal static (bool? Live, double Duration, string Protocol) ReadResolverMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024)
            return default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return default;
            bool? live = root.TryGetProperty("is_live", out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean() : null;
            double duration = root.TryGetProperty("duration", out value) && value.TryGetDoubleSafe(out double seconds) ? seconds : 0;
            string protocol = root.TryGetProperty("protocol", out value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
            bool hls = protocol.Contains("m3u8", StringComparison.OrdinalIgnoreCase);
            bool dash = protocol.Contains("dash", StringComparison.OrdinalIgnoreCase);
            if (root.TryGetProperty("requested_formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
                foreach (var item in formats.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("protocol", out value) && value.ValueKind == JsonValueKind.String)
                    {
                        string fragmentProtocol = value.GetString()!;
                        hls |= fragmentProtocol.Contains("m3u8", StringComparison.OrdinalIgnoreCase);
                        dash |= fragmentProtocol.Contains("dash", StringComparison.OrdinalIgnoreCase);
                    }
            // Only retain a known protocol category, never arbitrary extractor text.
            protocol = hls ? "m3u8" : dash ? "dash" : "";
            return (live, duration, protocol);
        }
        catch (JsonException) { return default; }
    }
}

internal static class CacheJsonNumber
{
    internal static bool TryGetDoubleSafe(this JsonElement value, out double number)
    {
        number = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out number) && double.IsFinite(number);
    }
}

internal readonly record struct NetworkCacheSample(double Seconds, double InputBytesPerSecond,
    double BitrateBitsPerSecond, double BufferedSeconds, bool Paused, bool Seeking, bool Buffering);

// Event-loop confined. No timer, tasks, URL retention, or native handles.
internal sealed class NetworkCacheSession
{
    internal string CapacityEstimate { get; private set; } = "unknown";
    internal int BufferingCount { get; private set; }
    internal double BufferingSeconds { get; private set; }
    internal double? BufferingStarted { get; private set; }
    internal double LastChange { get; private set; }
    internal int CountAtLastChange { get; private set; }
    double _stableSince = double.NaN;
    int _reliableSamples;
    double _lastSample = double.NegativeInfinity;

    internal void Buffering(bool active, double seconds, bool eligible)
    {
        if (active && eligible && BufferingStarted is null)
        {
            BufferingStarted = seconds;
            BufferingCount++;
            _stableSince = seconds;
        }
        else if (!active && BufferingStarted is double started)
        {
            BufferingSeconds += Math.Max(0, seconds - started);
            BufferingStarted = null;
        }
    }

    internal double? Decide(NetworkCacheMedia media, NetworkCacheSample sample, double currentWait)
    {
        if (!double.IsFinite(sample.Seconds) || sample.Seconds - _lastSample < 5)
            return null;
        if (sample.Seconds - _lastSample > 15)
        {
            _reliableSamples = 0;
            _stableSince = sample.Seconds;
        }
        _lastSample = sample.Seconds;
        bool reliable = double.IsFinite(sample.InputBytesPerSecond) && sample.InputBytesPerSecond > 0 &&
            double.IsFinite(sample.BitrateBitsPerSecond) && sample.BitrateBitsPerSecond > 0 &&
            double.IsFinite(sample.BufferedSeconds) && sample.BufferedSeconds >= 0;
        if (!reliable || sample.Paused || sample.Seeking || sample.Buffering)
        {
            CapacityEstimate = "unknown";
            _reliableSamples = 0;
            _stableSince = sample.Seconds;
            return null;
        }
        if (++_reliableSamples < 3 || sample.Seconds - LastChange < 30)
            return null;
        double ratio = sample.InputBytesPerSecond * 8 / sample.BitrateBitsPerSecond;
        CapacityEstimate = ratio < 1 ? "insufficient" : ratio < 1.15 ? "marginal" : "adequate";
        if (ratio < 1.15) // Insufficient or uncertain capacity: a larger wait is no cure.
        {
            _stableSince = sample.Seconds;
            return null;
        }
        double baseline = media.IsVod ? 3 : 1;
        double ceiling = media.IsVod ? 8 : 2;
        double? next = null;
        if (BufferingCount - CountAtLastChange >= 2 && currentWait < ceiling)
            next = Math.Min(ceiling, currentWait + 1);
        else if (sample.Seconds - _stableSince >= 120 && sample.BufferedSeconds >= currentWait * 2 && currentWait > baseline)
            next = Math.Max(baseline, currentWait - 1);
        if (next.HasValue)
        {
            LastChange = sample.Seconds;
            CountAtLastChange = BufferingCount;
            _stableSince = sample.Seconds;
        }
        return next;
    }
}
