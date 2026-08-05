using System.Threading;
using System.Threading.Tasks;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.Native;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    protected override void OnLogMessage(mpv_event_log_message data)
    {
        if (data.log_level == mpv_log_level.MPV_LOG_LEVEL_INFO)
        {
            string prefix = ConvertFromUtf8(data.prefix);

            if (prefix == "bd")
                ProcessBluRayLogMessage(ConvertFromUtf8(data.text));
        }

        base.OnLogMessage(data);
    }

    protected override void OnEndFile(mpv_event_end_file data)
    {
        mpv_end_file_reason reason = (mpv_end_file_reason)data.reason;
        string errorText = GetError((mpv_error)data.error);
        string failedPath = GetPropertyString("path");
        int failedPosition = GetPropertyInt("playlist-pos");
        int playlistCount = GetPropertyInt("playlist-count");
        bool playbackFailed = reason == mpv_end_file_reason.MPV_END_FILE_REASON_ERROR;
        Log.Debug($"mpv end-file event. reason={reason}, error={data.error}, errorText='{errorText}', path='{Log.SafeValue(failedPath)}', playlistPos={failedPosition}, playlistCount={playlistCount}");

        if (playbackFailed)
        {
            Log.Error($"Media playback failed; continuing with the next playlist item when available. error='{errorText}', path='{Log.SafeValue(failedPath)}', playlistPos={failedPosition}, playlistCount={playlistCount}");
            SchedulePlaybackErrorRecovery(failedPosition, failedPath);
        }

        if (playbackFailed &&
            errorText == "unrecognized file format" &&
            FileTypes.IsStreamingUrl(failedPath))
        {
            string hint = IsYouTubeUrl(failedPath)
                ? "YouTube playback usually depends on yt-dlp resolving the stream; browser cookies, an authenticated session, or in some cases a PO Token may be required."
                : "Streaming playback usually depends on yt-dlp or another resolver being able to access the URL.";

            Log.Error($"Streaming playback failed to resolve. url='{Log.SafeValue(failedPath)}', hint='{hint}'");
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
        Log.Debug($"mpv file-loaded event. path='{Log.SafeValue(GetPropertyString("path"))}', duration={Duration}, mediaTitle='{Log.SafeValue(GetPropertyString("media-title"))}'");

        if (App.StartSize == "video")
            WasInitialSizeSet = false;

        SchedulePlayerTask(_ => UpdateTracks());

        base.OnFileLoaded();
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
    {
        if (!Uri.TryCreate(path, UriKind.Absolute, out Uri? uri))
            return false;

        return uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("www.youtu.be", StringComparison.OrdinalIgnoreCase);
    }
}
