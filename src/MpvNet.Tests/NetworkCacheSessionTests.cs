using System;
using Xunit;

namespace MpvNet.Tests;

public sealed class NetworkCacheSessionTests
{
    [Theory]
    [InlineData("m3u8_native", false, NetworkMediaKind.Hls, true, false)]
    [InlineData("http_dash_segments", false, NetworkMediaKind.Dash, true, false)]
    [InlineData("m3u8_native", true, NetworkMediaKind.Hls, false, true)]
    [InlineData("http_dash_segments", true, NetworkMediaKind.Dash, false, true)]
    public void ResolverEvidenceClassifiesEdlWithoutFetchingOrLoggingSignedUrls(string protocol, bool live, NetworkMediaKind kind, bool vod, bool expectedLive)
    {
        var metadata = NetworkCacheMedia.ReadResolverMetadata($"{{\"is_live\":{live.ToString().ToLowerInvariant()},\"duration\":3600,\"protocol\":\"{protocol}\",\"url\":\"https://signed.invalid/?token=secret\"}}");
        var media = NetworkCacheMedia.Identify("https://youtube.com/watch?v=example", "edl://!no_clip;...", "edl", metadata.Duration, false, metadata.Live, metadata.Protocol);
        Assert.Equal(kind, media.Kind);
        Assert.Equal(vod, media.IsVod);
        Assert.Equal(expectedLive, media.IsLive);
        Assert.DoesNotContain("secret", metadata.Protocol);
    }

    [Theory]
    [InlineData("hls")]
    [InlineData("dash")]
    public void FiniteLiveWindowDoesNotImplyVod(string format)
    {
        var media = NetworkCacheMedia.Identify("https://example.com/opaque", "", format, 120, true);
        Assert.False(media.IsVod);
        Assert.False(media.IsLive); // Unknown, not a confirmed live.
        Assert.True(NetworkCacheMedia.Identify("https://example.com/opaque", "", format, 120, true, eof: true).IsVod);
    }

    [Fact]
    public void ProgressiveDurationAndSeekabilityConfirmVodButLocalFilesRemainLocal()
    {
        Assert.True(NetworkCacheMedia.Identify("https://example.com/file.mp4", "", "mov,mp4", 120, true).IsVod);
        Assert.False(NetworkCacheMedia.Identify("https://example.com/file.mp4", "", "mov,mp4", 0, true).IsVod);
        Assert.Equal(NetworkMediaKind.None, NetworkCacheMedia.Identify(@"C:\local.mp4", "https://example.com/a.m3u8", "hls", 120, true, false).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("[]")]
    [InlineData("{\"duration\":\"secret\",\"is_live\":\"false\",\"protocol\":123}")]
    public void MalformedResolverMetadataIsOptional(string json)
    {
        var metadata = NetworkCacheMedia.ReadResolverMetadata(json);
        Assert.Null(metadata.Live);
        Assert.Equal(0, metadata.Duration);
    }

    [Fact]
    public void RequestedFormatsCanIdentifyHlsBehindAnOpaqueResolvedUrl()
    {
        var metadata = NetworkCacheMedia.ReadResolverMetadata("{\"is_live\":false,\"requested_formats\":[{\"protocol\":\"m3u8_native\"},{\"protocol\":\"https\"}]}");
        Assert.Equal("m3u8", metadata.Protocol);
    }

    [Fact]
    public void OversizedResolverResultsFallBackWithoutParsingThePayload()
    {
        var metadata = NetworkCacheMedia.ReadResolverMetadata(new string('x', 4 * 1024 * 1024 + 1));
        Assert.Null(metadata.Live);
        Assert.Equal(0, metadata.Duration);
    }

    [Fact]
    public void BufferingMetricsExcludeStartupPauseSeekAndDuplicateNotifications()
    {
        var session = new NetworkCacheSession();
        session.Buffering(true, 0, false);
        Assert.Equal(0, session.BufferingCount);
        session.Buffering(true, 10, true);
        session.Buffering(true, 12, true);
        session.Buffering(false, 14, false);
        session.Buffering(false, 20, true);
        Assert.Equal(1, session.BufferingCount);
        Assert.Equal(4, session.BufferingSeconds);
        Assert.Null(session.BufferingStarted);
    }

    [Theory]
    [InlineData(true, 3, 4)]
    [InlineData(false, 1, 2)]
    public void OscillatingNetworkIncreasesWaitOnlyAfterRepeatedBufferingAndReliableSamples(bool vod, double current, double expected)
    {
        var session = SessionWithTwoUnderruns();
        var media = new NetworkCacheMedia(NetworkMediaKind.Hls, vod, !vod);
        Assert.Null(session.Decide(media, Sample(20), current));
        Assert.Null(session.Decide(media, Sample(25), current));
        Assert.Equal(expected, session.Decide(media, Sample(30), current));
        Assert.Null(session.Decide(media, Sample(35), expected)); // cooldown, no repeated increase
        Assert.Null(session.Decide(media, Sample(65), expected)); // no new underruns
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(double.NaN)]
    public void InsufficientOrUnavailableInputNeverTriggersIncrease(double input)
    {
        var session = SessionWithTwoUnderruns();
        var media = new NetworkCacheMedia(NetworkMediaKind.HttpProgressive, true, false);
        for (int seconds = 20; seconds < 120; seconds += 5)
            Assert.Null(session.Decide(media, Sample(seconds) with { InputBytesPerSecond = input }, 3));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void PauseSeekAndActiveBufferingPreventAdaptation(bool paused, bool seeking, bool buffering)
    {
        var session = SessionWithTwoUnderruns();
        for (int seconds = 20; seconds < 120; seconds += 5)
            Assert.Null(session.Decide(new(NetworkMediaKind.Hls, true, false), Sample(seconds) with
                { Paused = paused, Seeking = seeking, Buffering = buffering }, 3));
    }

    [Fact]
    public void StableNetworkReturnsToBaselineWithHysteresisAndNeverGrowsMemory()
    {
        var session = new NetworkCacheSession();
        var media = new NetworkCacheMedia(NetworkMediaKind.HttpProgressive, true, false);
        for (int seconds = 0; seconds < 120; seconds += 5)
            Assert.Null(session.Decide(media, Sample(seconds), 4));
        Assert.Equal(3, session.Decide(media, Sample(125), 4));
        Assert.Null(session.Decide(media, Sample(130), 3));
        Assert.Null(session.Decide(media, Sample(250), 3));
    }

    [Fact]
    public void SparseSamplesCannotProveTwoMinutesOfStableNetwork()
    {
        var session = new NetworkCacheSession();
        var media = new NetworkCacheMedia(NetworkMediaKind.HttpProgressive, true, false);
        Assert.Null(session.Decide(media, Sample(100), 4));
        Assert.Null(session.Decide(media, Sample(105), 4));
        Assert.Null(session.Decide(media, Sample(125), 4));
    }

    [Fact]
    public void LiveWaitIsCappedAndFreshSessionHasNoOldMetrics()
    {
        var session = SessionWithTwoUnderruns();
        for (int seconds = 20; seconds <= 60; seconds += 5)
            Assert.Null(session.Decide(new(NetworkMediaKind.Hls, false, true), Sample(seconds), 2));
        var next = new NetworkCacheSession();
        Assert.Equal(0, next.BufferingCount);
        Assert.Equal(0, next.BufferingSeconds);
        Assert.Null(next.Decide(new(NetworkMediaKind.Hls, true, false), Sample(120), 3));
    }

    static NetworkCacheSession SessionWithTwoUnderruns()
    {
        var session = new NetworkCacheSession();
        session.Buffering(true, 5, true); session.Buffering(false, 7, true);
        session.Buffering(true, 10, true); session.Buffering(false, 12, true);
        return session;
    }
    static NetworkCacheSample Sample(double seconds) => new(seconds, 2000, 8000, 20, false, false, false);
}
