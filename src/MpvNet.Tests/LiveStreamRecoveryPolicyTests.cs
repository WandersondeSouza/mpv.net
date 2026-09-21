using System;
using MpvNet;
using Xunit;

namespace MpvNet.Tests;

public sealed class LiveStreamRecoveryPolicyTests
{
    static readonly StreamingFailureDiagnostic TransientFailure = new(
        StreamingFailureCategory.Timeout, "network", "timed out", "retry", true, 1);

    [Theory]
    [InlineData(NetworkMediaKind.Rtsp)]
    [InlineData(NetworkMediaKind.Rtmp)]
    [InlineData(NetworkMediaKind.DatagramLive)]
    public void ContinuousProtocolsUseBoundedExponentialBackoff(NetworkMediaKind kind)
    {
        Assert.Equal(TimeSpan.FromSeconds(1), Evaluate(kind, completedAttempts: 0).Delay);
        Assert.Equal(TimeSpan.FromSeconds(2), Evaluate(kind, completedAttempts: 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(4), Evaluate(kind, completedAttempts: 2).Delay);
        Assert.False(Evaluate(kind, completedAttempts: 3).ShouldRetry);
    }

    [Theory]
    [InlineData(NetworkMediaKind.Hls)]
    [InlineData(NetworkMediaKind.Dash)]
    public void ManifestRequiresEvidenceThatItLoadedAsLive(NetworkMediaKind kind)
    {
        Assert.False(Evaluate(kind, fileWasLoaded: false, duration: TimeSpan.Zero).ShouldRetry);
        Assert.False(Evaluate(kind, fileWasLoaded: true, duration: TimeSpan.FromMinutes(10)).ShouldRetry);
        Assert.True(Evaluate(kind, fileWasLoaded: true, duration: TimeSpan.Zero).ShouldRetry);
    }

    [Theory]
    [InlineData(NetworkMediaKind.HttpProgressive)]
    [InlineData(NetworkMediaKind.GenericNetwork)]
    [InlineData(NetworkMediaKind.None)]
    public void VodAndUnknownSourcesAreNotRetried(NetworkMediaKind kind)
    {
        Assert.False(Evaluate(kind).ShouldRetry);
    }

    [Fact]
    public void PlaylistAndNonTransientFailuresAreNotRetried()
    {
        Assert.False(Evaluate(NetworkMediaKind.Rtsp, playlistCount: 2).ShouldRetry);
        Assert.False(LiveStreamRecoveryPolicy.Evaluate(
            NetworkMediaKind.Rtsp,
            true,
            TimeSpan.Zero,
            1,
            TransientFailure with { IsLikelyTransient = false },
            0).ShouldRetry);
    }

    static LiveStreamRecoveryDecision Evaluate(
        NetworkMediaKind kind,
        bool fileWasLoaded = true,
        TimeSpan duration = default,
        int playlistCount = 1,
        int completedAttempts = 0) =>
        LiveStreamRecoveryPolicy.Evaluate(
            kind, fileWasLoaded, duration, playlistCount, TransientFailure, completedAttempts);
}
