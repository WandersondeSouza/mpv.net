
using CommunityToolkit.Mvvm.Messaging;
using System.Text;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.MVVM;

namespace MpvNet;

public class AppClass
{
    const int SelectMenuVersion = 3;

    public List<string> TempFiles { get; } = new ();

    public string TempFolder => TemporaryFileCleanup.DefaultTempFolder + Path.DirectorySeparatorChar;
    public string ConfPath { get => Player.ConfigFolder + "mpvnet.conf"; }
    public string ProcessInstance { get; set; } = "single";
    public string DarkMode { get; set; } = "always";
    public string DarkTheme { get; set; } = "dark";
    public string LightTheme { get; set; } = "light";
    public string StartSize { get; set; } = "height-session";
    public string Language { get; set; } = LocalizationService.ResolveStartupLanguage();
    public string CommandLine { get; set; } = Environment.CommandLine;
    public string MenuSyntax { get; set; } = "#menu:";

    public bool AutoLoadFolder { get; set; }
    public bool DebugMode { get; set; }
    public bool Exit { get; set; }
    public bool IsTerminalAttached { get; } = Environment.GetEnvironmentVariable("_started_from_console") == "yes";
    public bool MediaInfo { get; set; } = true;
    public bool Queue { get; set; }
    public bool RememberAudioDevice { get; set; } = true;
    public bool RememberVolume { get; set; } = true;
    public bool RememberWindowPosition { get; set; }

    public int RecentCount { get; set; } = 15;

    public float AutofitAudio { get; set; } = 0.7f;
    public float AutofitImage { get; set; } = 0.8f;
    public float MinimumAspectRatio { get; set; }
    public float MinimumAspectRatioAudio { get; set; }

    readonly ExtensionLoader _extensionManager = new ExtensionLoader();

    AppSettings? _settings;

    public AppClass()
    {
        _extensionManager.UnhandledException += ex =>
        {
            Log.Error(ex, "Extension failed with an unhandled exception.");
            Terminal.WriteError(ex);
        };

        StrongReferenceMessenger.Default.Register<MainWindowIsLoadedMessage>(this, (r, msg) =>
        {
            TaskHelp.Run(() => _extensionManager.LoadFolder(Player.ConfigFolder + "extensions"));
        });
    }

    public AppSettings Settings => _settings ??= SettingsManager.Load();

    public void Init()
    {
        Log.Info("Initializing application configuration.");
        TemporaryFileCleanup.CleanupDefaultFolders();
        var useless1 = Player.ConfigFolder;
        EnsureInitialMpvConf();
        var useless2 = Player.Conf;

        foreach (var i in Conf)
            ProcessProperty(i.Key, i.Value, true);

        EnsureInitialSelectMenuConf();

        if (DebugMode)
        {
            string filePath = Player.ConfigFolder + "MpvNet-debug.log";

            if (File.Exists(filePath))
                File.Delete(filePath);

            Trace.Listeners.Add(new TextWriterTraceListener(filePath));
            Trace.AutoFlush = true;
        }

        Player.Shutdown += Player_Shutdown;
        Player.Initialized += Player_Initialized;
        Log.Info("Application configuration initialized.");
    }

    public static string About => "MPV.NET Media Player\n" +
        "Repository: https://github.com/WandersondeSouza/mpv.net\n" +
        "Maintainer: Wanderson Estanislau de Souza Rodrigues\n" +
        "Based on the original mpv.net project by Frank Skare / stax76\n" +
        "Copyright (C) 2000-2024 mpv.net/mpv/mplayer\n" +
        $"{AppInfo.Product} v{AppInfo.Version}" + GetLastWriteTime(Environment.ProcessPath!) + "\n" +
        $"{Player.GetPropertyString("mpv-version")}" + GetLastWriteTime(Folder.Startup + "libmpv-2.dll") + "\n" +
        $"ffmpeg {Player.GetPropertyString("ffmpeg-version")}\n" +
        $"MediaInfo v{FileVersionInfo.GetVersionInfo(Folder.Startup + "MediaInfo.dll").FileVersion}" +
        $"{GetLastWriteTime(Folder.Startup + "MediaInfo.dll")}" + "\n" + "GPL v2 License";

    static string GetLastWriteTime(string path) => $" ({File.GetLastWriteTime(path).ToShortDateString()})";

    void EnsureInitialMpvConf()
    {
        string appDataConfigFolder = (Folder.AppData + "mpv.net").Separator();

        if (!StringComparer.OrdinalIgnoreCase.Equals(Player.ConfigFolder, appDataConfigFolder))
            return;

        if (File.Exists(Player.ConfPath))
            return;

        File.WriteAllText(Player.ConfPath,
            "# Initial mpv/mpv.net configuration." + BR +
            "# This file is created only when no user mpv.conf exists." + BR +
            BR +
            "# Profile used by IPTV Media Center when it launches mpv.net." + BR +
            "# It is applied only with: --profile=iptv-media-center" + BR +
            "[iptv-media-center]" + BR +
            "force-window=yes" + BR +
            "idle=no" + BR +
            "cache=yes" + BR +
            "volume=100" + BR);
    }

    public void EnsureInitialSelectMenuConf()
    {
        string path = Player.ConfigFolder + "menu.conf";
        string culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        string header = $"# mpv.net autogenerated select.lua menu; culture={culture}; version={SelectMenuVersion}";

        if (File.Exists(path))
        {
            string content = File.ReadAllText(path);

            if (content.StartsWith(header, StringComparison.Ordinal))
                return;

            if (!content.StartsWith("# mpv.net autogenerated select.lua menu", StringComparison.Ordinal))
                return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, BuildSelectMenuConf(culture), new UTF8Encoding(false));
    }

    string BuildSelectMenuConf(string culture)
    {
        StringBuilder sb = new();

        sb.AppendLine($"# mpv.net autogenerated select.lua menu; culture={culture}; version={SelectMenuVersion}");
        sb.AppendLine("# Delete this file to regenerate the default localized menu.");
        sb.AppendLine();

        AddMenuItem("Subtitle Tracks", "script-binding select/select-sid");
        AddMenuItem("Secondary Subtitle", "script-binding select/select-secondary-sid");
        AddMenuItem("Subtitle Lines", "script-binding select/select-subtitle-line");
        AddMenuItem("Audio Tracks", "script-binding select/select-aid");
        AddMenuItem("Video Tracks", "script-binding select/select-vid");
        AddMenuItem("Playlist", "script-binding select/select-playlist");
        AddMenuItem("Chapters", "script-binding select/select-chapter");
        AddMenuItem("Editions", "script-binding select/select-edition");
        AddMenuItem("Audio Devices", "script-binding select/select-audio-device");
        AddMenuItem("Key bindings", "script-binding select/select-binding");
        AddMenuItem("Watch History", "script-binding select/select-watch-history");
        AddMenuItem("Watch Later", "script-binding select/select-watch-later");
        AddMenuItem("Playback statistics", "script-binding stats/display-page-1-toggle");
        AddMenuItem("File information", "script-binding stats/display-page-5-toggle");
        AddMenuItem("Edit config file", "script-binding select/edit-config-file");
        AddMenuItem("Edit key bindings", "script-binding select/edit-input-conf");
        AddMenuItem("Help", "script-binding stats/display-page-4-toggle");
        AddMenuItem("Online documentation", "script-binding select/open-docs");
        AddMenuItem("Support", "script-message-to mpvnet shell-execute mailto:wanderson_souza@hotmail.com");

        return sb.ToString();

        void AddMenuItem(string label, string command) =>
            sb.AppendLine($"{_(label)}\t{command}");
    }

    void Player_Initialized()
    {
        Log.Info("Player initialized.");

        if (RememberVolume)
        {
            Player.SetPropertyInt("volume", Settings.Volume);
            Player.SetPropertyString("mute", Settings.Mute);
        }

        if (RememberAudioDevice && Settings.AudioDevice != "")
            Player.SetPropertyString("audio-device", Settings.AudioDevice);

        EnsureInitialSelectMenuConf();
    }

    void Player_Shutdown()
    {
        Log.Info("Player shutting down.");
        Settings.Volume = Player.GetPropertyInt("volume");
        Settings.Mute = Player.GetPropertyString("mute");

        SettingsManager.Save(Settings);

        foreach (string file in TempFiles)
            FileHelp.Delete(file);
    }

    Dictionary<string, string>? _Conf;

    public Dictionary<string, string> Conf {
        get {
            if (_Conf == null)
            {
                _Conf = File.Exists(ConfPath)
                    ? ConfigFileParser.ParseKeyValueLines(File.ReadAllLines(ConfPath))
                    : [];
            }

            return _Conf;
        }
    }

    public bool ProcessProperty(string name, string value, bool writeError = false)
    {
        switch (name)
        {
            case "auto-load-folder": AutoLoadFolder = value == "yes"; return true;
            case "autofit-audio": AutofitAudio = value.Trim('%').ToInt(70) / 100f; return true;
            case "autofit-image": AutofitImage = value.Trim('%').ToInt(80) / 100f; return true;
            case "dark-mode": DarkMode = value; return true;
            case "dark-theme": DarkTheme = value.Trim('\'', '"'); return true;
            case "debug-mode": DebugMode = value == "yes"; return true;
            case "language":
                Language = string.Equals(value, "system", StringComparison.OrdinalIgnoreCase)
                    ? LocalizationService.ResolveStartupLanguage()
                    : LocalizationService.ResolveMpvNetLanguage(value);
                return true;
            case "light-theme": LightTheme = value.Trim('\'', '"'); return true;
            case "media-info": MediaInfo = value == "yes"; return true;
            case "menu-syntax": MenuSyntax = value; return true;
            case "minimum-aspect-ratio-audio": MinimumAspectRatioAudio = value.ToFloat(); return true;
            case "minimum-aspect-ratio": MinimumAspectRatio = value.ToFloat(); return true;
            case "process-instance": ProcessInstance = value; return true;
            case "queue": Queue = value == "yes"; return true;
            case "recent-count": RecentCount = value.ToInt(15); return true;
            case "remember-audio-device": RememberAudioDevice = value == "yes"; return true;
            case "remember-volume": RememberVolume = value == "yes"; return true;
            case "remember-window-position": RememberWindowPosition = value == "yes"; return true;
            case "start-size": StartSize = value; return true;

            default:
                if (writeError)
                {
                    Log.Debug($"Unknown mpv.net configuration property: {name}");
                    Terminal.WriteError($"unknown MpvNet.conf property: {name}");
                }

                return false;
        }
    }

    public static (string Title, string Path) GetTitleAndPath(string input)
    {
        if (input.Contains('|'))
        {
            var a = input.Split('|');
            return (a[1], a[0]);
        }

        return (input, input);
    }

    InputConf? _inputConf;

    public InputConf InputConf => _inputConf ??= new InputConf(Player.ConfigFolder + "input.conf");

    public void ApplyShowMenuFix()
    {
        if (Settings.ShowMenuFixApplied)
            return;

        if (File.Exists(InputConf.Path))
        {
            string content = File.ReadAllText(InputConf.Path);

            if (!content.Contains("script-message mpvnet show-menu") &&
                !content.Contains("script-message-to mpvnet show-menu"))

                File.WriteAllText(InputConf.Path, BR + content.Trim() + BR +
                    "MBTN_Right script-message-to mpvnet show-menu" + BR);
        }

        Settings.ShowMenuFixApplied = true;
    }

    public void ApplyInputDefaultBindingsFix()
    {
        if (Settings.InputDefaultBindingsFixApplied)
            return;

        if (File.Exists(Player.ConfPath))
        {
            string content = File.ReadAllText(Player.ConfPath);

            if (content.Contains("input-default-bindings = no"))
                File.WriteAllText(ConfPath, content.Replace("input-default-bindings = no", ""));

            if (content.Contains("input-default-bindings=no"))
                File.WriteAllText(ConfPath, content.Replace("input-default-bindings=no", ""));
        }

        Settings.InputDefaultBindingsFixApplied = true;
    }
}
