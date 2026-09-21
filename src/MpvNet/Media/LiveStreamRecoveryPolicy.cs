namespace MpvNet;

public readonly record struct LiveStreamRecoveryDecision(bool ShouldRetry, int Attempt, TimeSpan Delay)
{
    public static LiveStreamRecoveryDecision NoRetry => new(false, 0, TimeSpan.Zero);
}

public static class LiveStreamRecoveryPolicy
{
    public const int MaximumAttempts = 3;

    public static LiveStreamRecoveryDecision Evaluate(
        NetworkMediaKind kind,
        bool fileWasLoaded,
        TimeSpan duration,
        int playlistCount,
        StreamingFailureDiagnostic? diagnostic,
        int completedAttempts)
    {
        if (playlistCount > 1 || completedAttempts >= MaximumAttempts ||
            diagnostic is not { IsLikelyTransient: true })
        {
            return LiveStreamRecoveryDecision.NoRetry;
        }

        bool continuousProtocol = kind is NetworkMediaKind.Rtsp or NetworkMediaKind.Rtmp or NetworkMediaKind.DatagramLive;
        bool confirmedManifestLive = kind is NetworkMediaKind.Hls or NetworkMediaKind.Dash &&
            fileWasLoaded && duration <= TimeSpan.Zero;
        if (!continuousProtocol && !confirmedManifestLive)
            return LiveStreamRecoveryDecision.NoRetry;

        int attempt = completedAttempts + 1;
        return new(true, attempt, TimeSpan.FromSeconds(1 << completedAttempts));
    }
}
