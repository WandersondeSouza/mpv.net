using System.Threading;
using System.Threading.Tasks;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.Native;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    internal override void OnLogMessage(MpvEventSnapshot data)
    {
        StreamingFailureDiagnostic? diagnostic = StreamingFailureDiagnostics.Classify(data.Prefix, data.Text);
        if (diagnostic is not null &&
            (_lastStreamingFailure is null || diagnostic.Priority >= _lastStreamingFailure.Priority))
        {
            _lastStreamingFailure = diagnostic;
        }

        if (data.LogLevel == mpv_log_level.MPV_LOG_LEVEL_INFO)
        {
            if (data.Prefix == "bd")
                ProcessBluRayLogMessage(data.Text);
        }

        base.OnLogMessage(data);
    }

    internal override void OnEndFile(MpvEventSnapshot data)
    {
        mpv_end_file_reason reason = (mpv_end_file_reason)data.EndFileReason;
        if (reason == mpv_end_file_reason.MPV_END_FILE_REASON_STOP)
            CancelLiveStreamRecovery();

        string errorText = GetError((mpv_error)data.EndFileError);
        string failedPath = GetPropertyString("path");
        int failedPosition = GetPropertyInt("playlist-pos");
        int playlistCount = GetPropertyInt("playlist-count");
        bool playbackFailed = reason == mpv_end_file_reason.MPV_END_FILE_REASON_ERROR;
        StreamingFailureDiagnostic? streamingDiagnostic = playbackFailed && FileTypes.IsStreamingUrl(failedPath)
            ? _lastStreamingFailure ?? StreamingFailureDiagnostics.FromMpvError(errorText)
            : null;
        bool reconnectScheduled = playbackFailed &&
            TryScheduleLiveStreamRecovery(failedPath, playlistCount, streamingDiagnostic);
        Log.Debug($"mpv end-file event. reason={reason}, error={data.EndFileError}, errorText='{errorText}', path='{Log.SafeValue(failedPath)}', playlistPos={failedPosition}, playlistCount={playlistCount}");

        if (playbackFailed)
        {
            string recovery = reconnectScheduled ? "bounded live reconnect" : "next playlist item when available";
            Log.Error($"Media playback failed. recovery='{recovery}', error='{errorText}', path='{Log.SafeValue(failedPath)}', playlistPos={failedPosition}, playlistCount={playlistCount}");
            if (!reconnectScheduled)
                SchedulePlaybackErrorRecovery(failedPosition, failedPath);
        }

        if (playbackFailed && FileTypes.IsStreamingUrl(failedPath))
        {
            StreamingFailureDiagnostic diagnostic = streamingDiagnostic ??
                StreamingFailureDiagnostics.FromMpvError(errorText);
            Log.Error($"Streaming playback failure. category={diagnostic.Category}; component='{diagnostic.Component}'; original='{diagnostic.OriginalMessage}'; action='{diagnostic.SuggestedAction}'; path='{Log.SafeValue(failedPath)}'");

            if (!reconnectScheduled)
            {
                string message = StreamingFailureDiagnostics.GetUserMessage(diagnostic.Category);
                CommandV("show-text", message, "5000");
            }
        }

        base.OnEndFile(data);
        FileEnded = !playbackFailed;
    }

    protected override void OnVideoReconfig()
    {
        UpdateVideoSize("dwidth", "dheight");
        base.OnVideoReconfig();
    }

    // executed before OnFileLoaded
    protected override void OnStartFile()
    {
        // An end-file REDIRECT may be emitted while mpv advances or rebuilds
        // the playlist. A new start-file means playback is active again.
        FileEnded = false;
        _lastStreamingFailure = null;
        _currentMediaWasLoaded = false;
        Path = GetPropertyString("path");
        NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(Path);
        Log.Debug($"mpv start-file event. path='{Log.SafeValue(Path)}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}, cacheKind={resolution.Kind}, cacheProfile={resolution.Profile}, cacheEnabled={resolution.IsEnabled}");
        base.OnStartFile();
        if (App.AutoLoadFolder && TryConsumeAutoLoadFolderRequest())
            SchedulePlayerTask(LoadFolderAsync);
    }

    void SchedulePlaybackErrorRecovery(int failedPosition, string failedPath)
    {
        SchedulePlayerTask(async cancellationToken =>
        {
            await Task.Delay(150, cancellationToken);

            int currentPosition = GetPropertyInt("playlist-pos");
            int playlistCount = GetPropertyInt("playlist-count");
            string currentPath = GetPropertyString("path");

            if (!ShouldAdvanceAfterPlaybackError(failedPosition, currentPosition, playlistCount) ||
                string.IsNullOrWhiteSpace(currentPath) ||
                GetPlaylistPathKey(currentPath) != GetPlaylistPathKey(failedPath))
                return;

            int nextPosition = failedPosition + 1;
            Log.Error($"Playback remained on failed item; advancing playlist. failedPath='{Log.SafeValue(failedPath)}', failedPosition={failedPosition}, nextPosition={nextPosition}, playlistCount={playlistCount}");
            SetPropertyInt("playlist-pos", nextPosition);
        });
    }

    // executed after OnStartFile
    protected override void OnFileLoaded()
    {
        Duration = GetSafeDuration();
        _currentMediaWasLoaded = true;
        Log.Debug($"mpv file-loaded event. path='{Log.SafeValue(GetPropertyString("path"))}', duration={Duration}, mediaTitle='{Log.SafeValue(GetPropertyString("media-title"))}'");

        if (App.StartSize == "video")
            WasInitialSizeSet = false;

        SchedulePlayerTask(_ => UpdateTracks());

        base.OnFileLoaded();
    }

    bool TryScheduleLiveStreamRecovery(
        string failedPath,
        int playlistCount,
        StreamingFailureDiagnostic? diagnostic)
    {
        NetworkMediaKind kind = MediaInputClassifier.Classify(failedPath).NetworkKind;
        long generation;
        LiveStreamRecoveryDecision decision;

        lock (_mediaLoadStateLock)
        {
            generation = _mediaLoadGeneration;
            decision = LiveStreamRecoveryPolicy.Evaluate(
                kind,
                _currentMediaWasLoaded,
                Duration,
                playlistCount,
                diagnostic,
                _liveReconnectAttempts);
            if (!decision.ShouldRetry || _scheduledLiveReconnectGeneration != 0)
                return false;

            _liveReconnectAttempts = decision.Attempt;
            _scheduledLiveReconnectGeneration = generation;
        }

        StreamingFailureCategory category = diagnostic?.Category ?? StreamingFailureCategory.Unknown;
        Log.Error($"Transient live stream failure; reconnect scheduled. kind={kind}, attempt={decision.Attempt}/{LiveStreamRecoveryPolicy.MaximumAttempts}, delaySeconds={decision.Delay.TotalSeconds:0}, category={category}, path='{Log.SafeValue(failedPath)}'");
        Task delayedReconnect = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(decision.Delay, PlayerCancellationToken);
                SchedulePlayerTask(_ =>
                {
                    try
                    {
                        lock (_mediaLoadStateLock)
                        {
                            if (_mediaLoadGeneration != generation)
                                return;

                            string currentPath = GetPropertyString("path");
                            if (string.IsNullOrWhiteSpace(currentPath) ||
                                GetPlaylistPathKey(currentPath) != GetPlaylistPathKey(failedPath))
                            {
                                return;
                            }

                            Log.Error($"Retrying transient live stream. kind={kind}, attempt={decision.Attempt}/{LiveStreamRecoveryPolicy.MaximumAttempts}, path='{Log.SafeValue(failedPath)}'");
                            SendLoadfile(failedPath, 0, false);
                        }
                    }
                    finally
                    {
                        lock (_mediaLoadStateLock)
                            if (_scheduledLiveReconnectGeneration == generation)
                                _scheduledLiveReconnectGeneration = 0;
                    }
                });
            }
            catch (OperationCanceledException) when (PlayerCancellationToken.IsCancellationRequested)
            {
            }
        }, PlayerCancellationToken);
        TrackEventTask(delayedReconnect);
        return true;
    }

    void ProcessBluRayLogMessage(string msg)
    {
        lock (BluRayTitles)
        {
            if (msg.Contains(" 0 duration: "))
                BluRayTitles.Clear();

            if (msg.Contains(" duration: "))
            {
                int start = msg.IndexOf(" duration: ") + 11;
                BluRayTitles.Add(new TimeSpan(
                    msg.Substring(start, 2).ToInt(),
                    msg.Substring(start + 3, 2).ToInt(),
                    msg.Substring(start + 6, 2).ToInt()));
            }
        }
    }

    internal static bool IsYouTubeUrl(string path)
        => YouTubeMediaPolicy.Analyze(path).IsYouTube;
}
