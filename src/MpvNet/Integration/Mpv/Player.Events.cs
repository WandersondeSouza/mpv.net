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
        string errorText = GetError((mpv_error)data.error);
        Log.Debug($"mpv end-file event. reason={(mpv_end_file_reason)data.reason}, error={data.error}, errorText='{errorText}', path='{Log.SafeValue(GetPropertyString("path"))}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}");

        if ((mpv_end_file_reason)data.reason == mpv_end_file_reason.MPV_END_FILE_REASON_ERROR &&
            errorText == "unrecognized file format" &&
            FileTypes.IsStreamingUrl(Path))
        {
            string hint = IsYouTubeUrl(Path)
                ? "YouTube playback usually depends on yt-dlp resolving the stream; browser cookies, an authenticated session, or in some cases a PO Token may be required."
                : "Streaming playback usually depends on yt-dlp or another resolver being able to access the URL.";

            Log.Error($"Streaming playback failed to resolve. url='{Log.SafeValue(Path)}', hint='{hint}'");
        }

        base.OnEndFile(data);
        FileEnded = true;
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
        Log.Debug($"mpv start-file event. path='{Log.SafeValue(Path)}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}");
        base.OnStartFile();
        BackgroundTaskRunner.Run(LoadFolder);
    }

    // executed after OnStartFile
    protected override void OnFileLoaded()
    {
        Duration = GetSafeDuration();
        Log.Debug($"mpv file-loaded event. path='{Log.SafeValue(GetPropertyString("path"))}', duration={Duration}, mediaTitle='{Log.SafeValue(GetPropertyString("media-title"))}'");

        if (App.StartSize == "video")
            WasInitialSizeSet = false;

        BackgroundTaskRunner.Run(UpdateTracks);

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
