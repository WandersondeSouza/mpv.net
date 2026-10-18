using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
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
    [InlineData("Opening https://manifest.example/api/manifest/hls_playlist/ip/private-value/signature/private-value/playlist/index.m3u8")]
    [InlineData("> GET /videoplayback/id/video/ip/private-value/lsig/private-value/file/seg.ts HTTP/1.1")]
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

    [Fact]
    public void SignedPathIsRedactedBeforeLongDiagnosticIsSplit()
    {
        string url = "https://cdn.example/videoplayback/id/" + new string('x', 600) +
            "/ip/private-address/lsig/private-signature/file/seg.ts";
        string sanitized = string.Join("\n", StreamingFailureDiagnostics.FormatDiagnosticLines("Opening " + url));
        Assert.DoesNotContain("private-", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 50), sanitized, StringComparison.Ordinal);
        Assert.Equal("https://cdn.example/[redacted]", Log.SafeValue(url));
        Assert.Equal("Opening https://cdn.example/[redacted]", StreamingFailureDiagnostics.SanitizeMessage("Opening " + url));
        Assert.DoesNotContain("private-", StreamingFailureDiagnostics.SanitizeMessage("> GET /videoplayback/ip/private-address/lsig/private-signature HTTP/1.1"), StringComparison.Ordinal);
    }

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
    [InlineData("ERROR: Private video. Login required", StreamingFailureCategory.Authentication, false)]
    [InlineData("ERROR: Sign in to confirm you're not a bot. Use --cookies", StreamingFailureCategory.BotVerification, false)]
    [InlineData("ERROR: Sign in to confirm you’re not a bot. HTTP Error 403", StreamingFailureCategory.BotVerification, false)]
    [InlineData("Sign in to confirm you are not a bot", StreamingFailureCategory.BotVerification, false)]
    [InlineData("Sign in to confirm you\uFFFDre not a bot", StreamingFailureCategory.BotVerification, false)]
    [InlineData("Sign in to confirm you\u0092re not a bot", StreamingFailureCategory.BotVerification, false)]
    [InlineData("ERROR: JavaScript challenge solving failed", StreamingFailureCategory.JavaScriptChallenge, false)]
    [InlineData("A PO Token is required for this client", StreamingFailureCategory.ProofOfOriginToken, false)]
    [InlineData("Could not resolve host: example.com", StreamingFailureCategory.NetworkDns, true)]
    [InlineData("HTTP Error 503: Service Unavailable", StreamingFailureCategory.Http, true)]
    [InlineData("< HTTP/2 429", StreamingFailureCategory.Http, true)]
    [InlineData("HTTP status: 408", StreamingFailureCategory.Http, true)]
    [InlineData("HTTP/2 404 Not Found; size 503", StreamingFailureCategory.Http, false)]
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
    public void BotVerificationWinsOverGenericCookieAndHttpErrorsWithoutLeakingSecrets()
    {
        StreamingFailureDiagnostic diagnostic = StreamingFailureDiagnostics.Classify("ytdl_hook",
            "HTTP Error 403: Sign in to confirm you’re not a bot. cookies are required; https://youtube.com/watch?v=private-value")!;
        Assert.Equal(StreamingFailureCategory.BotVerification, diagnostic.Category);
        Assert.False(diagnostic.IsLikelyTransient);
        Assert.DoesNotContain("private-value", diagnostic.OriginalMessage);
        Assert.Contains("same network", diagnostic.SuggestedAction);
        Assert.Contains("FFmpeg", diagnostic.SuggestedAction);
        Assert.False(LiveStreamRecoveryPolicy.Evaluate(NetworkMediaKind.Rtsp, true,
            TimeSpan.Zero, 1, diagnostic, 0).ShouldRetry);
    }

    [Theory]
    [InlineData("curl", "transfer failed: Failure when receiving data from the peer", "NetworkTransport", true)]
    [InlineData("curl", "transfer failed: Failed sending data to the peer", "NetworkTransport", true)]
    [InlineData("curl", "transfer failed: Failed receiving data from the peer", "NetworkTransport", true)]
    [InlineData("curl", "ngtcp2_conn_writev_stream returned error: ERR_DRAINING", "NetworkTransport", true)]
    [InlineData("ffmpeg/video", "vp9: Failed to read frame header.", "Decoder", false)]
    [InlineData("ffmpeg/video", "vp9: Invalid frame marker", "Decoder", false)]
    [InlineData("ffmpeg/video", "vp9: Failed to read unit 0 (type 0): Invalid data found when processing input.", "Decoder", false)]
    [InlineData("vd", "Error while decoding frame!", "Decoder", false)]
    public void RecordedTransportAndDecoderFailuresAreClassifiedSeparately(
        string component, string message, string category, bool transient)
    {
        StreamingFailureDiagnostic? diagnostic = StreamingFailureDiagnostics.Classify(component, message);
        Assert.NotNull(diagnostic);
        Assert.Equal(category, diagnostic.Category.ToString());
        Assert.Equal(transient, diagnostic.IsLikelyTransient);
    }

    [Theory]
    [InlineData("ffmpeg/demuxer", "vp9: Invalid frame marker")]
    [InlineData("ytdl_hook", "Selected vp9 format")]
    [InlineData("curl", "proto=https ok=1 code=200 size=400")]
    public void InformationalOrUnrelatedMessagesDoNotImplyTransportOrDecoderFailure(string prefix, string text)
    {
        Assert.Null(StreamingFailureDiagnostics.Classify(prefix, text));
    }

    [Theory]
    [InlineData("transfer failed: Failure when receiving data from the peer")]
    [InlineData("transfer failed: Failed sending data to the peer")]
    public void RuntimeErrorLoggingFollowsBuildSwitchAndResetsForNextMedia(string transferError)
    {
        string marker = "runtime-error-" + Guid.NewGuid().ToString("N");
        bool debug = Global.App.DebugMode;
        using RuntimeFailurePlayer player = new();
        string url = "https://cdn.example/video/lsig/private-runtime-signature";
        try
        {
            Global.App.DebugMode = false;
            player.Path = url;
            MpvNet.MpvEventSnapshot decoder = CreateLogSnapshot("ffmpeg/video", "vp9: Failed to read frame header. " + marker + "\r\n");
            MpvNet.MpvEventSnapshot network = CreateLogSnapshot("curl", transferError + " " + marker + "\n");
            for (int count = 0; count < 5; count++)
            {
                player.OnLogMessage(decoder);
                player.OnLogMessage(network);
            }
            Assert.Equal(url, player.Path);
            string log = System.IO.Path.Combine(Log.LogFolder, "mpvnet-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            string[] summaries = ReadSummaries();
            if (Log.IsEnabled)
            {
                Assert.Equal(2, summaries.Length);
                Assert.Contains(summaries, line => line.Contains("category=Decoder", StringComparison.Ordinal));
                Assert.Contains(summaries, line => line.Contains("category=NetworkTransport", StringComparison.Ordinal));
                Assert.All(summaries, line => Assert.DoesNotContain("private-runtime-signature", line, StringComparison.Ordinal));
                Assert.All(summaries, line => Assert.Contains("'; action='", line, StringComparison.Ordinal));
            }
            else
                Assert.Empty(summaries);

            player.StartNextMedia();
            player.Path = url;
            player.OnLogMessage(decoder);
            Assert.Equal(Log.IsEnabled ? 3 : 0, ReadSummaries().Length);

            string[] ReadSummaries() => File.Exists(log)
                ? File.ReadAllLines(log).Where(line => line.Contains(marker, StringComparison.Ordinal)).ToArray()
                : [];
        }
        finally
        {
            Global.App.DebugMode = debug;
        }
    }

    sealed class RuntimeFailurePlayer : MainPlayer
    {
        public void StartNextMedia() => OnStartFile();
    }

    static MpvNet.MpvEventSnapshot CreateLogSnapshot(string component, string message)
    {
        nint prefix = Marshal.StringToCoTaskMemUTF8(component);
        nint text = Marshal.StringToCoTaskMemUTF8(message);
        nint payload = Marshal.AllocHGlobal(Marshal.SizeOf<mpv_event_log_message>());
        try
        {
            Marshal.StructureToPtr(new mpv_event_log_message
            {
                prefix = prefix, text = text, log_level = mpv_log_level.MPV_LOG_LEVEL_ERROR
            }, payload, false);
            return MpvNet.MpvEventSnapshot.Create(new mpv_event
            {
                event_id = mpv_event_id.MPV_EVENT_LOG_MESSAGE, data = payload
            });
        }
        finally
        {
            Marshal.FreeHGlobal(payload);
            Marshal.FreeCoTaskMem(prefix);
            Marshal.FreeCoTaskMem(text);
        }
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
    [InlineData(StreamingFailureCategory.BotVerification, "The source service requires verification that you are not a bot.")]
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
