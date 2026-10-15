using System;
using System.Linq;
using System.Text.Json;
using MpvNet;
using Xunit;

using static MpvNet.Native.LibMpv;

namespace MpvNet.Tests;

public sealed class StreamingFailureDiagnosticsTests
{
    [Fact]
    public void JsonEncodedYtDlpOutputPreservesLaterLinesAndLongMessages()
    {
        string longLine = "[debug] " + new string('x', 1200) + " END-OF-DIAGNOSTIC";
        string output = "[debug] yt-dlp version 2026.08.19\r\n" + longLine + "\n[info] Selected formats: 248+251";

        string decoded = StreamingFailureDiagnostics.DecodeDiagnosticString(JsonSerializer.Serialize(output));
        string[] lines = StreamingFailureDiagnostics.FormatDiagnosticLines(decoded).ToArray();

        Assert.Equal(output, decoded);
        Assert.Equal(5, lines.Length);
        Assert.All(lines, line => Assert.InRange(line.Length, 1, 500));
        Assert.Equal(longLine, string.Concat(lines.Skip(1).Take(3)));
        Assert.Equal("[info] Selected formats: 248+251", lines[^1]);
    }

    [Theory]
    [InlineData("plain output with C:\\tools\\yt-dlp.exe")]
    [InlineData("\"malformed JSON")]
    [InlineData("")]
    public void DiagnosticDecodeKeepsPlainOrMalformedText(string value) =>
        Assert.Equal(value, StreamingFailureDiagnostics.DecodeDiagnosticString(value));

    [Theory]
    [InlineData("[debug] Command-line config: ['--password', 'private-value']")]
    [InlineData("[debug] User config: ['--add-header', 'X-Secret: private-value']")]
    [InlineData("[debug] Extractor args: {'youtube': {'po_token': ['private-value']}}")]
    [InlineData("> Authorization: Bearer private-value more-private")]
    [InlineData("Authorization=Bearer private-value more-private")]
    [InlineData("< Set-Cookie: SID=private-value; Path=/; Secure")]
    [InlineData("> X-Custom-Secret: private-value")]
    [InlineData("* [HTTP/2] [1] [:path: /videoplayback?sig=private-value&expire=123]")]
    [InlineData("* [HTTP/2] [1] [x-custom-secret: private-value]")]
    [InlineData("> GET /videoplayback?sig=private-value HTTP/1.1")]
    [InlineData("Opening https://user:private-value@cdn.example/video?sig=private-value#secret")]
    public void DetailedDiagnosticsRedactArgumentsHeadersAndRequestTargets(string value)
    {
        string sanitized = string.Join("\n", StreamingFailureDiagnostics.FormatDiagnosticLines(value));
        Assert.DoesNotContain("private-value", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("more-private", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void LongDiagnosticRedactsBeforeSplittingAtChunkBoundary()
    {
        string value = new string('x', 490) + " https://cdn.example/video?signature=private-value " +
            new string('y', 600) + " END";
        string[] lines = StreamingFailureDiagnostics.FormatDiagnosticLines(value).ToArray();
        string sanitized = string.Concat(lines);

        Assert.True(lines.Length > 1);
        Assert.Contains("?[redacted]", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-value", sanitized, StringComparison.Ordinal);
        Assert.EndsWith(" END", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("< HTTP/2 206")]
    [InlineData("> Range: bytes=20971520-")]
    [InlineData("< Content-Range: bytes 20971520-41943039/90000000")]
    public void DetailedTransportDiagnosticsPreserveStatusAndByteRanges(string value) =>
        Assert.Equal(value, Assert.Single(StreamingFailureDiagnostics.FormatDiagnosticLines(value)));

    [Theory]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_ERROR, "ffmpeg/video", false, true)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_WARN, "curl", false, true)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_TRACE, "curl", true, true)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_DEBUG, "demux", true, true)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_DEBUG, "lavf", true, true)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_DEBUG, "ffmpeg/demuxer", true, true)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_TRACE, "demux", true, false)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_DEBUG, "ffmpeg/video", true, false)]
    [InlineData(mpv_log_level.MPV_LOG_LEVEL_TRACE, "curl", false, false)]
    public void ExtraMpvMessagesAreLimitedToDiagnosticTransportAndDemuxer(
        mpv_log_level level, string prefix, bool detailed, bool expected) =>
        Assert.Equal(expected, StreamingFailureDiagnostics.ShouldLogMpvMessage(level, prefix, detailed));

    [Theory]
    [InlineData("ERROR: Sign in to confirm your age; cookies are required", StreamingFailureCategory.Authentication, false)]
    [InlineData("ERROR: JavaScript challenge solving failed", StreamingFailureCategory.JavaScriptChallenge, false)]
    [InlineData("A PO Token is required for this client", StreamingFailureCategory.ProofOfOriginToken, false)]
    [InlineData("Could not resolve host: example.com", StreamingFailureCategory.NetworkDns, true)]
    [InlineData("HTTP Error 503: Service Unavailable", StreamingFailureCategory.Http, true)]
    [InlineData("TLS handshake failed: certificate verify failed", StreamingFailureCategory.TlsCertificate, false)]
    [InlineData("demuxer: unrecognized file format", StreamingFailureCategory.Demuxer, false)]
    public void ClassifiesStreamingFailureOnlyFromEvidence(
        string message,
        StreamingFailureCategory category,
        bool transient)
    {
        StreamingFailureDiagnostic? diagnostic = StreamingFailureDiagnostics.Classify("test", message);

        Assert.NotNull(diagnostic);
        Assert.Equal(category, diagnostic.Category);
        Assert.Equal(transient, diagnostic.IsLikelyTransient);
    }

    [Fact]
    public void UnknownMessageDoesNotInventACause()
    {
        Assert.Null(StreamingFailureDiagnostics.Classify("test", "something unusual happened"));
    }

    [Fact]
    public void DiagnosticMessageRedactsUrlsAndSecrets()
    {
        const string message = "failed https://cdn.example/video.m3u8?token=secret&sig=abc Authorization=BearerSecret cookie=session";

        string sanitized = StreamingFailureDiagnostics.SanitizeMessage(message);

        Assert.Contains("https://cdn.example/video.m3u8?[redacted]", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BearerSecret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("session", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void YtDlpVerboseOutputRedactsSignedUrlAndPoToken()
    {
        const string output = "[debug] Command-line config: ['--verbose', 'https://youtube.com/watch?v=video&token=private']\n[debug] po_token=private-token";

        string sanitized = StreamingFailureDiagnostics.SanitizeMessage(output);

        Assert.Contains("https://youtube.com/watch?[redacted]", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("private", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(StreamingFailureCategory.Authentication, "This content requires authentication with the source service.")]
    [InlineData(StreamingFailureCategory.RegionalRestriction, "This content is not available in your region.")]
    [InlineData(StreamingFailureCategory.ContentUnavailable, "This content is no longer available.")]
    [InlineData(StreamingFailureCategory.Playlist, "The online playlist could not be loaded.")]
    [InlineData(StreamingFailureCategory.StreamEnded, "The live stream has ended.")]
    [InlineData(StreamingFailureCategory.UnsupportedProtocol, "This online media format is not supported.")]
    [InlineData(StreamingFailureCategory.InvalidUrl, "The online media URL is invalid.")]
    [InlineData(StreamingFailureCategory.Extractor, "The online media could not be loaded.")]
    public void MapsDiagnosticsToStableLocalizedMessageIds(
        StreamingFailureCategory category,
        string expected)
    {
        Assert.Equal(expected, StreamingFailureDiagnostics.GetUserMessage(category));
    }
}
