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
        Log.Info($"mpv end-file event. reason={(mpv_end_file_reason)data.reason}, error={data.error}, errorText='{GetError((mpv_error)data.error)}', path='{Log.SafeValue(GetPropertyString("path"))}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}");
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
        Log.Info($"mpv start-file event. path='{Log.SafeValue(Path)}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}");
        base.OnStartFile();
        TaskHelp.Run(LoadFolder);
    }

    // executed after OnStartFile
    protected override void OnFileLoaded()
    {
        Duration = GetSafeDuration();
        Log.Info($"mpv file-loaded event. path='{Log.SafeValue(GetPropertyString("path"))}', duration={Duration}, mediaTitle='{Log.SafeValue(GetPropertyString("media-title"))}'");

        if (App.StartSize == "video")
            WasInitialSizeSet = false;

        TaskHelp.Run(UpdateTracks);

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
}
