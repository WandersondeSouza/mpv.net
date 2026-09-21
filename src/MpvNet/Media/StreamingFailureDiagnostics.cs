using System.Text.RegularExpressions;

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
    Playlist
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
    public static string GetUserMessage(StreamingFailureCategory category) => category switch
    {
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
        string safeMessage = SanitizeMessage(message);

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

        if (HttpStatusPattern().IsMatch(text))
            return Result(StreamingFailureCategory.Http, "HTTP source", safeMessage,
                "Check the reported HTTP status and whether the source URL is still valid.", IsTransientHttp(text), 55);

        if (ContainsAny(text, "connection reset", "connection was reset", "broken pipe", "connection closed", "end of file"))
            return Result(StreamingFailureCategory.StreamEnded, "network stream", safeMessage,
                "Retry only if this is a continuous live stream rather than normal end of media.", true, 50);

        if (ContainsAny(text, "protocol not found", "unsupported protocol", "protocol not supported"))
            return Result(StreamingFailureCategory.UnsupportedProtocol, "FFmpeg/mpv", safeMessage,
                "Confirm that the packaged FFmpeg/mpv build supports this protocol.", false, 45);

        if (ContainsAny(text, "failed to open playlist", "playlist error", "invalid playlist"))
            return Result(StreamingFailureCategory.Playlist, "mpv/demuxer", safeMessage,
                "Validate the playlist response and its referenced media entries.", false, 40);

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

    static bool IsTransientHttp(string message) =>
        message.Contains(" 408", StringComparison.Ordinal) ||
        message.Contains(" 429", StringComparison.Ordinal) ||
        Http5xxPattern().IsMatch(message);

    [GeneratedRegex("https?://[^\\s'\"<>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"(?i)\b(cookie|authorization|po[_ -]?token|session(?:id)?|signature|sig|token)\s*[:=]\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"\b(?:http (?:error|status)\s*)?[45]\d\d\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HttpStatusPattern();

    [GeneratedRegex(@"\b5\d\d\b", RegexOptions.CultureInvariant)]
    private static partial Regex Http5xxPattern();
}
