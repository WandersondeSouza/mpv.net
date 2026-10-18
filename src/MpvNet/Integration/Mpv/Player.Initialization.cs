using System.Threading.Tasks;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.Native;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    internal const string YtDlpScriptOption = "ytdl_hook-ytdl_path";
    string? _automaticYtDlpPath;
    bool _keepUserYtDlpPath;

    void ConfigureYtDlpPath() => ConfigureYtDlpPath(RuntimeComponents.ResolveComponent("yt-dlp.exe"));

    internal void ConfigureYtDlpPath(ComponentResolutionResult resolution)
    {
        if (!_mpvInitialized || _keepUserYtDlpPath ||
            !TryGetPropertyStringMap("options/script-opts", out Dictionary<string, string> options))
            return;

        bool hasPath = options.TryGetValue(YtDlpScriptOption, out string? currentPath);
        if ((_automaticYtDlpPath is null && hasPath) ||
            (_automaticYtDlpPath is not null && (!hasPath || currentPath != _automaticYtDlpPath)) ||
            HasYtDlpScriptConfiguration())
        {
            _keepUserYtDlpPath = true;
            Log.Debug("Keeping explicit ytdl_hook-ytdl_path from mpv configuration or runtime changes.");
            return;
        }

        if (resolution is not { IsValid: true, ResolvedPath: not null })
        {
            Log.Debug($"No validated yt-dlp executable was resolved. reason='{Log.SafeValue(resolution.DiagnosticMessage)}'");
            return;
        }

        if (currentPath == resolution.ResolvedPath)
            return;

        // change-list append accepts a single literal key=value argument;
        // it does not parse commas inside the value as additional options.
        string value = resolution.ResolvedPath;
        CommandV("change-list", "script-opts", "append", $"{YtDlpScriptOption}={value}");
        if (TryGetPropertyStringMap("options/script-opts", out options) &&
            options.TryGetValue(YtDlpScriptOption, out string? configuredPath) && configuredPath == value)
        {
            _automaticYtDlpPath = value;
            Log.Debug($"Configured mpv ytdl_hook-ytdl_path from resolved component. source={resolution.Source}, path='{Log.SafeValue(value)}'");
        }
    }

    bool HasYtDlpScriptConfiguration()
    {
        string folder = TryGetPropertyString("options/config-dir", out string configuredFolder) &&
            !string.IsNullOrWhiteSpace(configuredFolder) ? configuredFolder : ConfigFolder;
        string path = System.IO.Path.Combine(folder, "script-opts", "ytdl_hook.conf");
        try
        {
            return File.Exists(path) && ConfigFileParser.ParseKeyValueLines(
                File.ReadLines(path).Select(line => line.TrimStart())).ContainsKey("ytdl_path");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug($"Could not inspect ytdl_hook.conf; keeping script configuration. error='{Log.SafeValue(ex.Message)}'");
            return true;
        }
    }

    public void RefreshYtDlpPathAfterComponentBootstrap()
    {
        if (!_mpvInitialized)
            return;

        SchedulePlayerTask(_ => ConfigureYtDlpPath());
    }

    bool HasConfiguredMpvOption(string optionName)
    {
        if (!File.Exists(ConfPath))
            return false;

        try
        {
            return File.ReadLines(ConfPath).Any(line =>
            {
                string value = line.TrimStart();
                return !value.StartsWith('#') &&
                    (value.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase) ||
                     value.Equals("no-" + optionName, StringComparison.OrdinalIgnoreCase));
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug($"Could not inspect mpv.conf for {optionName}. path='{Log.SafeValue(ConfPath)}', error='{Log.SafeValue(ex.Message)}'");
            return false;
        }
    }

    public void Init(IntPtr formHandle, bool processCommandLine)
    {
        RuntimeComponents.RegisterNativeResolver();
        LifecycleState = PlayerLifecycleState.Initializing;
        Log.Debug($"Initializing mpv player. formHandle={formHandle}, processCommandLine={processCommandLine}");
        App.ApplyShowMenuFix();

        MainHandle = mpv_create();
        Handle = MainHandle;

        if (MainHandle == IntPtr.Zero)
        {
            Log.Error("mpv_create failed.");
            LifecycleState = PlayerLifecycleState.Destroyed;
            throw new InvalidOperationException("libmpv could not create the main player handle.");
        }

        var events = Enum.GetValues<mpv_event_id>().Cast<mpv_event_id>();

        foreach (mpv_event_id i in events)
        {
            mpv_request_event(MainHandle, i, 0);
        }

        mpv_request_log_messages(MainHandle, "no");

        if (App.IsTerminalAttached)
        {
            Log.Debug("Terminal is attached; enabling mpv terminal input.");
            SetPropertyString("terminal", "yes");
            SetPropertyString("input-terminal", "yes");
        }

        if (formHandle != IntPtr.Zero)
        {
            Log.Debug("Configuring mpv for embedded window output.");
            SetPropertyString("force-window", "yes");
            SetPropertyLong("wid", formHandle.ToInt64());
        }

        SetPropertyInt("osd-duration", 2000);

        SetPropertyBool("input-default-bindings", true);
        SetPropertyBool("input-builtin-bindings", false);
        SetPropertyBool("input-media-keys", true);

        SetPropertyString("autocreate-playlist", "filter");
        // The desktop frontend owns SMTC for its HWND. Two publishers produce
        // competing Windows sessions. Explicit mpv.conf/CLI values still win.
        SetPropertyString("media-controls", formHandle == IntPtr.Zero ? "yes" : "no");
        SetPropertyString("idle", "yes");
        SetPropertyString("config-dir", ConfigFolder);
        Log.Debug($"Using mpv config directory: '{Log.SafeValue(ConfigFolder)}'");
        Directory.CreateDirectory(CacheFolder);
        Log.Debug($"Using mpv cache directory: '{Log.SafeValue(CacheFolder)}'");
        SetPropertyString("demuxer-cache-dir", CacheFolder);
        SetPropertyString("icc-cache-dir", CacheFolder);
        SetPropertyString("gpu-shader-cache-dir", CacheFolder);
        SetPropertyString("config", "yes");
        SetOptionString("load-context-menu", "no");
        SetPropertyString("screenshot-directory", "~~desktop/");

        SetPropertyString("osd-msg1", "${?playlist-playing-pos==-1:" + _("Drop files or URLs to play here.") + "}");
        SetPropertyString("osd-playing-msg", "${media-title}");
        SetPropertyString("osc", "no");
        
        UsedInputConfContent = App.InputConf.GetContent();

        if (!string.IsNullOrEmpty(UsedInputConfContent))
        {
            Log.Debug($"Loading input.conf from memory. path='{Log.SafeValue(App.InputConf.Path)}', length={UsedInputConfContent.Length}");
            SetPropertyString("input-conf", @"memory://" + UsedInputConfContent);
        }

        if (processCommandLine)
            CommandLine.ProcessCommandLineArgsPreInit();

        if (CommandLine.Contains("config-dir"))
        {
            string configDir = CommandLine.GetValue("config-dir");
            string fullPath = System.IO.Path.GetFullPath(configDir);
            App.InputConf.Path = fullPath.Separator() + "input.conf";
            string content = App.InputConf.GetContent();
            Log.Debug($"Command line config-dir changed input.conf path. configDir='{Log.SafeValue(configDir)}', resolved='{Log.SafeValue(App.InputConf.Path)}', length={content.Length}");

            if (!string.IsNullOrEmpty(content))
                SetPropertyString("input-conf", @"memory://" + content);
        }

        Environment.SetEnvironmentVariable("MPVNET_VERSION", AppInfo.Version.ToString());  // deprecated

        mpv_error err = mpv_initialize(MainHandle);

        if (err < 0)
        {
            string error = GetError(err);
            Log.Error("mpv_initialize failed: " + error);
            throw new InvalidOperationException($"libmpv initialization failed ({err}): {error}");
        }

        SetMpvInitialized();
        SetPropertyString("user-data/mpvnet-donation-url", AppClass.GetDonationUrl(App.Language));
        SetPropertyString("user-data/mpvnet-website-url", AppClass.GetOfficialWebsiteUrl(App.Language));
        CommandV("change-list", "script-opts", "append", "osc-idlescreen=no");
        CommandV("change-list", "script-opts", "append", "osc-custom_button_1_content={\\fnSegoe UI Symbol}\u2665");
        CommandV(
            "change-list", "script-opts", "append",
            "osc-custom_button_1_mbtn_left_command=expand-properties script-message-to mpvnet shell-execute ${user-data/mpvnet-donation-url}");
        CommandV("change-list", "script-opts", "append", "osc-custom_button_2_content={\\fnSegoe UI Symbol}\U0001F310");
        CommandV(
            "change-list", "script-opts", "append",
            "osc-custom_button_2_mbtn_left_command=expand-properties script-message-to mpvnet shell-execute ${user-data/mpvnet-website-url}");
        CommandV("load-script", System.IO.Path.Combine(AppContext.BaseDirectory, "Scripts", "osc.lua"));

        if (formHandle != IntPtr.Zero)
            TrackEventTask(Task.Run(() => MainEventLoop(PlayerCancellationToken), PlayerCancellationToken));

        CommandV("script-message", "osc-idlescreen", "no", "silent");

        string idle = GetPropertyString("idle");
        App.Exit = idle == "no" || idle == "once";
        Log.Debug($"mpv initialized. idle='{idle}', appExitOnIdle={App.Exit}, processCommandLine={processCommandLine}");

        Handle = mpv_create_client(MainHandle, "mpvnet");

        if (Handle == IntPtr.Zero)
        {
            Log.Error("mpv_create_client failed.");
            throw new InvalidOperationException("libmpv could not create the mpvnet client handle.");
        }

        // Read effective CLI/profile/mpv.conf options after mpv_initialize.
        ConfigureYtDlpPath();

        mpv_request_log_messages(Handle, "info");

        if (App.DebugMode && Log.DiagnosticFileLoggingEnabled)
        {
            if (!CommandLine.Contains("msg-level") && !HasConfiguredMpvOption("msg-level") &&
                TryGetPropertyString("options/msg-level", out string configuredLevels) &&
                string.IsNullOrWhiteSpace(configuredLevels))
                SetPropertyString("msg-level", StreamingFailureDiagnostics.DiagnosticMpvLevels);

            // terminal-default honors per-module verbosity without enabling
            // per-packet/frame trace messages across the entire player.
            mpv_request_log_messages(Handle, "terminal-default");
            Log.Debug("Enabled transport/demuxer diagnostics; explicit msg-level settings take precedence.");
        }

        if (App.DebugMode && Log.DiagnosticFileLoggingEnabled)
        {
            CommandV("change-list", "ytdl-raw-options", "append", "verbose=");
            Log.Debug("Enabled yt-dlp verbose output for diagnostic logging.");
        }

        // Prepare titles and YouTube playlist options before ytdl_hook (priority 10).
        RegisterHook("on_load", PlaylistTitleHookUserData, 5);
        RegisterHook("on_load", NetworkCacheHookUserData, 50);

        if (formHandle != IntPtr.Zero)
            TrackEventTask(Task.Run(() => EventLoop(PlayerCancellationToken), PlayerCancellationToken));

        // otherwise shutdown is raised before media files are loaded,
        // this means Lua scripts that use idle might not work correctly
        SetPropertyString("idle", "yes");
        Log.Debug("Reset mpv idle property to yes after mpvnet client creation.");

        SetPropertyString("user-data/frontend/name", "mpv.net");
        SetPropertyString("user-data/frontend/version", AppInfo.Version.ToString());
        SetPropertyString("user-data/frontend/process-path", Environment.ProcessPath!);

        ConfigureObservedProperties();

        Initialized?.Invoke();
        LifecycleState = PlayerLifecycleState.Running;
        Log.Debug("mpv player initialized.");
    }
}
