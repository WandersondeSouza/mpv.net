using System;
using MpvNet;
using Xunit;

namespace MpvNet.Tests;

public sealed class StreamingFailureDiagnosticsTests
{
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
