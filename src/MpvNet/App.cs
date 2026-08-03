
using CommunityToolkit.Mvvm.Messaging;
using System.Text;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.MVVM;

namespace MpvNet;

public class AppClass
{
    const int SelectMenuVersion = 4;
    const string DonationPortalUrl = "https://www.gestaodesistemas.com.br/doar/mpvnet";

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
    public bool AutomaticNetworkCache { get; set; } = true;
    public bool DebugMode { get; set; }
    public bool Exit { get; set; }
    public bool IsTerminalAttached { get; } = Environment.GetEnvironmentVariable("_started_from_console") == "yes";
    public bool MediaInfo { get; set; } = true;
    public bool Queue { get; set; }
    public bool RememberAudioDevice { get; set; } = true;
    public bool RememberVolume { get; set; } = true;
    public bool RememberWindowPosition { get; set; }

    public int RecentCount { get; set; } = 15;

    public string NetworkCacheProfile { get; set; } = "balanced";

    public float AutofitAudio { get; set; } = 0.7f;
    public float AutofitImage { get; set; } = 0.8f;
    public float MinimumAspectRatio { get; set; }
    public float MinimumAspectRatioAudio { get; set; }

    readonly ExtensionService _extensionService = new();

    AppSettings? _settings;

    public AppClass()
    {
        _extensionService.UnhandledException += ex =>
        {
            Log.Error(ex, "Extension failed with an unhandled exception.");
            Terminal.WriteError(ex);
        };

        StrongReferenceMessenger.Default.Register<MainWindowIsLoadedMessage>(this, (r, msg) =>
        {
            BackgroundTaskRunner.Run(() => _extensionService.LoadFolder(Player.ConfigFolder + "extensions"));
        });
    }

    public AppSettings Settings => _settings ??= SettingsStore.Load();

    public void Init()
    {
        Log.Debug("Initializing application configuration.");
        TemporaryFileCleanup.CleanupDefaultFolders();
        string resolvedConfigFolder = Player.ConfigFolder;
        EnsureInitialMpvConf();
        Dictionary<string, string> loadedPlayerConfiguration = Player.Conf;
        Log.Debug(
            $"Player configuration initialized. folder='{Log.SafeValue(resolvedConfigFolder)}', " +
            $"propertyCount={loadedPlayerConfiguration.Count}");

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
        Log.Debug("Application configuration initialized.");
    }

    public static string About => "MPV.NET Media Player\n" +
        "Repository: https://github.com/WandersondeSouza/mpv.net\n" +
        "Maintainer: Wanderson Estanislau de Souza Rodrigues\n" +
        "Based on the original mpv.net project by Frank Skare / stax76\n" +
        $"{_("Tip: installing Codec Guide can improve codec support and playback.")}\n" +
        "Copyright (C) 2000-2024 mpv.net/mpv/mplayer\n" +
        $"{AppInfo.Product} v{AppInfo.Version}" + GetLastWriteTime(Environment.ProcessPath!) + "\n" +
        $"{Player.GetPropertyString("mpv-version")}" + GetLastWriteTime(RuntimeComponents.ResolveComponentPath("libmpv-2.dll")) + "\n" +
        $"ffmpeg {Player.GetPropertyString("ffmpeg-version")}\n" +
        $"MediaInfo v{FileVersionInfo.GetVersionInfo(RuntimeComponents.ResolveComponentPath("MediaInfo.dll")).FileVersion}" +
        $"{GetLastWriteTime(RuntimeComponents.ResolveComponentPath("MediaInfo.dll"))}" + "\n" + "GPL v2 License";

    public static string CodecGuideTip => _("Tip: installing Codec Guide can improve codec support and playback.");
    public static string DonationLinkTitle => _("Make a donation to help maintain the project.");
    public static string DonationLinkDescription => _("Your donation helps keep MPV.NET Media Player in development, with improvements, fixes, and ongoing project support.");
    public static string GitHubSponsorsUrl => "https://github.com/sponsors/stax76";
    public string DonationUrl => GetDonationUrl(Language);

    public static string GetDonationUrl(string? language)
    {
        string resolved = LocalizationService.ResolveMpvNetLanguage(language);
        string publicLanguage = resolved.ToLowerInvariant() switch
        {
            "portuguese-brazil" => "pt-BR",
            "portuguese-portugal" => "pt-PT",
            "chinese-china" => "zh-CN",
            "bulgarian" => "bg",
            "german" => "de",
            "spanish" => "es",
            "french" => "fr",
            "italian" => "it",
            "japanese" => "ja",
            "korean" => "ko",
            "polish" => "pl",
            "russian" => "ru",
            "turkish" => "tr",
            _ => "en"
        };
        return $"{DonationPortalUrl}?language={Uri.EscapeDataString(publicLanguage)}&source=desktop-app";
    }

    static string GetLastWriteTime(string path) => $" ({File.GetLastWriteTime(path).ToShortDateString()})";

    void EnsureInitialMpvConf()
    {
        string defaultConfigFolder = AppPaths.WithTrailingSeparator(AppPaths.DefaultConfig);

        if (!StringComparer.OrdinalIgnoreCase.Equals(Player.ConfigFolder, defaultConfigFolder))
            return;

        if (File.Exists(Player.ConfPath))
            return;

        FileHelp.WriteAllTextAtomic(Player.ConfPath,
            "# Initial mpv/mpv.net configuration." + BR +
            "# This file is created only when no user mpv.conf exists." + BR +
            BR +
            "# Streaming URLs may receive the conservative network-cache policy configured in mpvnet.conf." + BR);
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
        FileHelp.WriteAllTextAtomic(path, BuildSelectMenuConf(culture), new UTF8Encoding(false));
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
        AddMenuItem("Support", "-");
        AddMenuItem("GitHub Sponsors", $"script-message-to mpvnet shell-execute {GitHubSponsorsUrl}");

        AddMenuItem("Donation", $"script-message-to mpvnet shell-execute {DonationUrl}");

        AddMenuItem("E-mail support", "script-message-to mpvnet shell-execute mailto:wanderson_souza@hotmail.com");

        return sb.ToString();

        void AddMenuItem(string label, string command) =>
            sb.AppendLine($"{_(label)}\t{command}");
    }

    void Player_Initialized()
    {
        Log.Debug("Player initialized.");

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
        Log.Debug("Player shutting down.");
        Settings.Volume = Player.GetPropertyInt("volume");
        Settings.Mute = Player.GetPropertyString("mute");

        SettingsStore.Save(Settings);

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
            case "automatic-network-cache": AutomaticNetworkCache = value == "yes"; return true;
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
            case "network-cache-profile": NetworkCacheProfile = NetworkCachePolicy.NormalizeProfile(value); return true;
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
            {
                File.Copy(InputConf.Path, InputConf.Path + ".backup", true);
                FileHelp.WriteAllTextAtomic(InputConf.Path, BR + content.Trim() + BR +
                    "MBTN_Right script-message-to mpvnet show-menu" + BR);
            }
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
            string updatedContent = content
                .Replace("input-default-bindings = no", "")
                .Replace("input-default-bindings=no", "");

            if (updatedContent != content)
            {
                File.Copy(Player.ConfPath, Player.ConfPath + ".backup", true);
                FileHelp.WriteAllTextAtomic(Player.ConfPath, updatedContent);
            }
        }

        Settings.InputDefaultBindingsFixApplied = true;
    }
}
