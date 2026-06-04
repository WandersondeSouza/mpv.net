using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.Native;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    public void Init(IntPtr formHandle, bool processCommandLine)
    {
        Log.Info("Initializing mpv player.");
        App.ApplyShowMenuFix();

        MainHandle = mpv_create();
        Handle = MainHandle;

        var events = Enum.GetValues<mpv_event_id>().Cast<mpv_event_id>();

        foreach (mpv_event_id i in events)
        {
            mpv_request_event(MainHandle, i, 0);
        }

        mpv_request_log_messages(MainHandle, "no");

        if (formHandle != IntPtr.Zero)
            TaskHelp.Run(MainEventLoop);

        if (MainHandle == IntPtr.Zero)
        {
            Log.Error("mpv_create failed.");
            throw new Exception("error mpv_create");
        }

        if (App.IsTerminalAttached)
        {
            SetPropertyString("terminal", "yes");
            SetPropertyString("input-terminal", "yes");
        }

        if (formHandle != IntPtr.Zero)
        {
            SetPropertyString("force-window", "yes");
            SetPropertyLong("wid", formHandle.ToInt64());
        }

        SetPropertyInt("osd-duration", 2000);

        SetPropertyBool("input-default-bindings", true);
        SetPropertyBool("input-builtin-bindings", false);
        SetPropertyBool("input-media-keys", true);

        SetPropertyString("autocreate-playlist", "filter");
        SetPropertyString("media-controls", "yes");
        SetPropertyString("idle", "yes");
        SetPropertyString("config-dir", ConfigFolder);
        Directory.CreateDirectory(CacheFolder);
        SetPropertyString("demuxer-cache-dir", CacheFolder);
        SetPropertyString("icc-cache-dir", CacheFolder);
        SetPropertyString("gpu-shader-cache-dir", CacheFolder);
        SetPropertyString("config", "yes");
        SetOptionString("load-context-menu", "no");
        SetPropertyString("screenshot-directory", "~~desktop/");
        SetOptionString("script-opts-append", "osc-idlescreen=no");
        SetPropertyString("osd-msg1", "${?playlist-playing-pos==-1:" + _("Drop files or URLs to play here.") + "}");
        SetPropertyString("osd-playing-msg", "${media-title}");
        SetPropertyString("osc", "yes");
        
        UsedInputConfContent = App.InputConf.GetContent();

        if (!string.IsNullOrEmpty(UsedInputConfContent))
            SetPropertyString("input-conf", @"memory://" + UsedInputConfContent);

        if (processCommandLine)
            CommandLine.ProcessCommandLineArgsPreInit();

        if (CommandLine.Contains("config-dir"))
        {
            string configDir = CommandLine.GetValue("config-dir");
            string fullPath = System.IO.Path.GetFullPath(configDir);
            App.InputConf.Path = fullPath.Separator() + "input.conf";
            string content = App.InputConf.GetContent();

            if (!string.IsNullOrEmpty(content))
                SetPropertyString("input-conf", @"memory://" + content);
        }

        Environment.SetEnvironmentVariable("MPVNET_VERSION", AppInfo.Version.ToString());  // deprecated

        mpv_error err = mpv_initialize(MainHandle);

        if (err < 0)
        {
            Log.Error("mpv_initialize failed: " + GetError(err));
            throw new Exception("mpv_initialize error" + BR2 + GetError(err) + BR);
        }

        CommandV("script-message", "osc-idlescreen", "no", "silent");

        string idle = GetPropertyString("idle");
        App.Exit = idle == "no" || idle == "once";

        Handle = mpv_create_client(MainHandle, "mpvnet");

        if (Handle == IntPtr.Zero)
        {
            Log.Error("mpv_create_client failed.");
            throw new Exception("mpv_create_client error");
        }

        mpv_request_log_messages(Handle, "info");

        if (formHandle != IntPtr.Zero)
            TaskHelp.Run(EventLoop);

        // otherwise shutdown is raised before media files are loaded,
        // this means Lua scripts that use idle might not work correctly
        SetPropertyString("idle", "yes");

        SetPropertyString("user-data/frontend/name", "mpv.net");
        SetPropertyString("user-data/frontend/version", AppInfo.Version.ToString());
        SetPropertyString("user-data/frontend/process-path", Environment.ProcessPath!);

        ConfigureObservedProperties();

        Initialized?.Invoke();
        Log.Info("mpv player initialized.");
    }
}
