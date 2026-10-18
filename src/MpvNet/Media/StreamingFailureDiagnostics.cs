using System.Text.Json;
using System.Text.RegularExpressions;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public enum StreamingFailureCategory
{
    Unknown,
    InvalidUrl,
    NetworkDns,
    Timeout,
    Http,
    ContentUnavailable,
    Authentication,
    RegionalRestriction,
    Extractor,
    JavaScriptChallenge,
    ProofOfOriginToken,
    BrowserImpersonation,
    TlsCertificate,
    StreamEnded,
    UnsupportedProtocol,
    Demuxer,
    Playlist,
    NetworkTransport,
    Decoder,
    BotVerification
}

public sealed record StreamingFailureDiagnostic(
    StreamingFailureCategory Category,
    string Component,
    string OriginalMessage,
    string SuggestedAction,
    bool IsLikelyTransient,
    int Priority);

public static partial class StreamingFailureDiagnostics
{
    internal const string DiagnosticMpvLevels = "curl=trace,demux=debug,lavf=debug,ffmpeg/demuxer=debug";

    internal static bool ShouldLogMpvMessage(mpv_log_level level, string prefix, bool detailed) =>
        level is mpv_log_level.MPV_LOG_LEVEL_FATAL or mpv_log_level.MPV_LOG_LEVEL_ERROR or mpv_log_level.MPV_LOG_LEVEL_WARN ||
        detailed && (prefix == "curl" ||
            level <= mpv_log_level.MPV_LOG_LEVEL_DEBUG &&
            prefix is "demux" or "lavf" or "ffmpeg/demuxer");

    // user-data properties can serialize a string as JSON, including escaped
    // newlines. Decode only a complete JSON string; leave plain text untouched.
    internal static string DecodeDiagnosticString(string value)
    {
        if (!value.TrimStart().StartsWith('"'))
            return value;

        try
        {
            return JsonSerializer.Deserialize<string>(value) ?? "";
        }
        catch (JsonException)
        {
            return value;
        }
    }

    internal static IEnumerable<string> FormatDiagnosticLines(string value)
    {
        foreach (string line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Verbose command-line dumps can contain cookies, passwords or
            // provider arguments. The remaining extractor output is sufficient.
            string sanitized = line.Contains(" config:", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Extractor args:", StringComparison.OrdinalIgnoreCase)
                ? "[debug] Configuration arguments: [redacted]"
                : SanitizeDiagnosticLine(line);

            // Redact before splitting so credentials spanning a chunk boundary
            // cannot leak. Preserve the whole sanitized message, not only its start.
            for (int offset = 0; offset < sanitized.Length; offset += 500)
                yield return sanitized.Substring(offset, Math.Min(500, sanitized.Length - offset));
        }
    }

    static string SanitizeDiagnosticLine(string line)
    {
        string sanitized = SensitiveHeaderPattern().Replace(line, "$1: [redacted]");
        sanitized = CurlHeaderPattern().Replace(sanitized, match =>
            match.Groups[2].Value.ToLowerInvariant() is "range" or "content-range" or "content-length" or
                "content-type" or "accept-ranges" or "transfer-encoding"
                ? match.Value : match.Groups[1].Value + "[header redacted]");
        sanitized = CurlHttp2HeaderPattern().Replace(sanitized, "[header redacted]");
        sanitized = UrlPattern().Replace(sanitized, match => SanitizeUrl(match.Value));
        sanitized = SignedRequestPathPattern().Replace(sanitized, "/[redacted]");
        sanitized = RelativeQueryPattern().Replace(sanitized, "$1?[redacted]");
        return SecretPattern().Replace(sanitized, "$1=[redacted]");
    }

    public static string GetUserMessage(StreamingFailureCategory category) => category switch
    {
        StreamingFailureCategory.BotVerification => _("The source service requires verification that you are not a bot."),
        StreamingFailureCategory.Authentication => _("This content requires authentication with the source service."),
        StreamingFailureCategory.RegionalRestriction => _("This content is not available in your region."),
        StreamingFailureCategory.ContentUnavailable => _("This content is no longer available."),
        StreamingFailureCategory.Playlist => _("The online playlist could not be loaded."),
        StreamingFailureCategory.StreamEnded => _("The live stream has ended."),
        StreamingFailureCategory.UnsupportedProtocol => _("This online media format is not supported."),
        StreamingFailureCategory.InvalidUrl => _("The online media URL is invalid."),
        _ => _("The online media could not be loaded.")
    };

    public static StreamingFailureDiagnostic? Classify(string? component, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        string text = message.ToLowerInvariant();
        // Windows subprocess stderr can contain a CP1252 smart apostrophe,
        // surfaced as U+0092 or U+FFFD after UTF-8 conversion by libmpv.
        string normalizedText = text.Replace('\u2019', '\'').Replace('\u0092', '\'').Replace('\uFFFD', '\'');
        // mpv log payloads normally end with a newline. Keep summaries and
        // their action/path on the same log line.
        string safeMessage = SanitizeMessage(message.Trim());

        if (ContainsAny(normalizedText, "confirm you're not a bot", "confirm you are not a bot"))
            return Result(StreamingFailureCategory.BotVerification, "yt-dlp/source service", safeMessage,
                "The service requested bot verification. Check access in a browser on the same network and the extractor's official guidance; use authorized cookie configuration only if needed. Updating FFmpeg does not resolve this challenge.", false, 105);

        if (ContainsAny(text, "po token", "po_token", "proof of origin"))
            return Result(StreamingFailureCategory.ProofOfOriginToken, "yt-dlp", safeMessage,
                "Configure a supported PO Token provider only when required by the extractor; do not paste tokens into logs.", false, 100);

        if (ContainsAny(text, "javascript challenge", "js challenge", "ejs", "n challenge", "signature solving failed"))
            return Result(StreamingFailureCategory.JavaScriptChallenge, "yt-dlp/EJS", safeMessage,
                "Run --diagnose-components and verify a supported JavaScript runtime and current yt-dlp.", false, 95);

        if (ContainsAny(text, "impersonat", "curl_cffi", "tls fingerprint"))
            return Result(StreamingFailureCategory.BrowserImpersonation, "yt-dlp optional impersonation", safeMessage,
                "Check whether the extractor explicitly requires an available impersonation target.", false, 90);

        if (ContainsAny(text, "private video", "login required", "sign in to confirm", "cookies are required", "authentication required", "http error 401", "http error 403"))
            return Result(StreamingFailureCategory.Authentication, "yt-dlp/source service", safeMessage,
                "Use an authorized yt-dlp cookie/provider configuration outside MPV.NET when access is legitimate.", false, 85);

        if (ContainsAny(text, "not available in your country", "geo restricted", "geographic restriction", "region restricted"))
            return Result(StreamingFailureCategory.RegionalRestriction, "source service", safeMessage,
                "Confirm that the media is available in the current region.", false, 85);

        if (ContainsAny(text, "video unavailable", "has been removed", "deleted video", "this video is unavailable"))
            return Result(StreamingFailureCategory.ContentUnavailable, "source service", safeMessage,
                "Verify that the media still exists and is publicly accessible.", false, 80);

        if (ContainsAny(text, "unable to extract", "extractor error", "no suitable extractor", "unsupported url", "update yt-dlp"))
            return Result(StreamingFailureCategory.Extractor, "yt-dlp", safeMessage,
                "Verify the effective yt-dlp version and the extractor's current support.", false, 75);

        if (ContainsAny(text, "certificate verify failed", "certificate verification", "tls handshake", "ssl error"))
            return Result(StreamingFailureCategory.TlsCertificate, "network/TLS", safeMessage,
                "Verify the system clock, certificate chain, proxy and TLS interception settings.", false, 70);

        if (ContainsAny(text, "could not resolve host", "failed to resolve", "name resolution", "no such host", "dns"))
            return Result(StreamingFailureCategory.NetworkDns, "network/DNS", safeMessage,
                "Check DNS and network connectivity, then retry.", true, 65);

        if (ContainsAny(text, "timed out", "timeout", "operation has timed out"))
            return Result(StreamingFailureCategory.Timeout, "network", safeMessage,
                "Retry after checking connectivity and the configured network timeout.", true, 60);

        if (component == "curl" && ContainsAny(text,
            "failure when receiving data from the peer", "failure when sending data to the peer",
            "failed receiving data from the peer", "failed sending data to the peer", "err_draining"))
            return Result(StreamingFailureCategory.NetworkTransport, "network/curl", safeMessage,
                "Inspect the negotiated HTTP version and preceding transfer errors; compare curl-http-version=2 for HTTP/3 failures.", true, 58);

        Match httpStatus = HttpStatusPattern().Match(text);
        if (httpStatus.Success)
            return Result(StreamingFailureCategory.Http, "HTTP source", safeMessage,
                "Check the reported HTTP status and whether the source URL is still valid.",
                httpStatus.Groups[1].Value is "408" or "429" || httpStatus.Groups[1].Value.StartsWith('5'), 55);

        if (ContainsAny(text, "connection reset", "connection was reset", "broken pipe", "connection closed", "end of file"))
            return Result(StreamingFailureCategory.StreamEnded, "network stream", safeMessage,
                "Retry only if this is a continuous live stream rather than normal end of media.", true, 50);

        if (ContainsAny(text, "protocol not found", "unsupported protocol", "protocol not supported"))
            return Result(StreamingFailureCategory.UnsupportedProtocol, "FFmpeg/mpv", safeMessage,
                "Confirm that the packaged FFmpeg/mpv build supports this protocol.", false, 45);

        if (ContainsAny(text, "failed to open playlist", "playlist error", "invalid playlist"))
            return Result(StreamingFailureCategory.Playlist, "mpv/demuxer", safeMessage,
                "Validate the playlist response and its referenced media entries.", false, 40);

        if (component is "ffmpeg/video" or "vd" && ContainsAny(text,
            "failed to read frame header", "invalid frame marker", "invalid data found when processing input",
            "error while decoding frame", "too large in superframe", "bitstream ended", "out of range"))
            return Result(StreamingFailureCategory.Decoder, "video decoder", safeMessage,
                "Compare the same media interval and source format; inspect transport errors before attributing the failure to the decoder.", false, 35);

        if (ContainsAny(text, "demuxer", "unrecognized file format", "failed to recognize file format"))
            return Result(StreamingFailureCategory.Demuxer, "mpv/demuxer", safeMessage,
                "Inspect earlier resolver and network messages before treating this as a media-format failure.", false, 30);

        if (ContainsAny(text, "invalid url", "malformed url", "url is invalid"))
            return Result(StreamingFailureCategory.InvalidUrl, component ?? "input", safeMessage,
                "Verify the URL without removing valid query parameters.", false, 25);

        return null;
    }

    public static StreamingFailureDiagnostic FromMpvError(string errorMessage) =>
        Classify("mpv", errorMessage) ??
        Result(StreamingFailureCategory.Unknown, "mpv", SanitizeMessage(errorMessage),
            "Inspect preceding mpv and yt-dlp diagnostic messages for a more specific cause.", false, 0);

    public static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "<none>";

        string sanitized = UrlPattern().Replace(message, match => SanitizeUrl(match.Value));
        sanitized = SignedRequestPathPattern().Replace(sanitized, "/[redacted]");
        sanitized = SecretPattern().Replace(sanitized, "$1=[redacted]");
        return Log.SafeValue(sanitized);
    }

    static string SanitizeUrl(string value)
    {
        string suffix = value.EndsWith('.') || value.EndsWith(',') || value.EndsWith(';') ? value[^1..] : "";
        string candidate = suffix.Length == 0 ? value : value[..^1];
        return Log.SafeValue(candidate) + suffix;
    }

    static StreamingFailureDiagnostic Result(
        StreamingFailureCategory category,
        string component,
        string message,
        string action,
        bool transient,
        int priority) => new(category, component, message, action, transient, priority);

    static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.Ordinal));

    [GeneratedRegex("https?://[^\\s'\"<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"(?i)\b(cookie|authorization|po[_ -]?token|session(?:id)?|signature|sig|token)\s*[:=]\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"\b(proxy-authorization|authorization|set-cookie|cookie|x-api-key)\s*[:=]\s*[^\r\n]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveHeaderPattern();

    [GeneratedRegex(@"^([<>]\s*)([\w-]+):[^\r\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex CurlHeaderPattern();

    [GeneratedRegex(@"\[:?[\w-]+:\s*[^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex CurlHttp2HeaderPattern();

    [GeneratedRegex("(/[^\\s'\"<>?]*)\\?[^\\s'\"<>]+", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeQueryPattern();

    [GeneratedRegex("(?<![\\w:/])(?=[^\\s'\"<>]*?/(?:signature|sig|lsig|token|access_token|auth|authorization|ip)/)/[^\\s'\"<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SignedRequestPathPattern();

    [GeneratedRegex(@"\bhttp(?:/\d(?:\.\d)?)?(?:\s+(?:error|status))?\s*[:=]?\s+([45]\d\d)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpStatusPattern();
}
