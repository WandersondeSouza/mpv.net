
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows;

using MpvNet.Extensions;
using MpvNet.Windows.WinForms;
using MpvNet.Windows.WPF.Views;
using MpvNet.Windows.WPF;
using MpvNet.Windows.WPF.MsgBox;
using MpvNet.Windows.Help;
using MpvNet.Help;

namespace MpvNet;

public class GuiCommand
{
    Dictionary<string, Action<IList<string>>>? _commands;

    public event Action<float>? ScaleWindow;
    public event Action<string>? MoveWindow;
    public event Action<float>? WindowScaleNet;
    public event Action? ShowMenu;

    public static GuiCommand Current { get; } = new();

    public Dictionary<string, Action<IList<string>>> Commands => _commands ??= new()
    {
        ["add-to-path"] = args => AddToPath(),
        ["edit-conf-file"] = EditCongFile,
        ["load-audio"] = LoadAudio,
        ["load-sub"] = LoadSubtitle,
        ["move-window"] = MoveWindowCommand,
        ["open-clipboard"] = OpenFromClipboard,
        ["open-files"] = OpenFiles,
        ["open-optical-media"] = Open_DVD_Or_BD_Folder,
        ["reg-file-assoc"] = RegisterFileAssociations,
        ["remove-from-path"] = args => RemoveFromPath(),
        ["scale-window"] = ScaleWindowCommand,
        ["show-about"] = args => ShowDialog(typeof(AboutWindow)),
        ["show-bindings"] = args => ShowBindings(),
        ["show-commands"] = args => ShowCommands(),
        ["show-conf-editor"] = args => ShowDialog(typeof(ConfWindow)),
        ["show-decoders"] = args => ShowDecoders(),
        ["show-demuxers"] = args => ShowDemuxers(),
        ["show-info"] = args => ShowMediaInfo(new[] { "osd" }),
        ["show-input-editor"] = args => ShowDialog(typeof(InputWindow)),
        ["show-keys"] = args => ShowKeys(),
        ["show-media-info"] = ShowMediaInfo,
        ["show-menu"] = args => ShowMenu?.Invoke(),
        ["show-profiles"] = args => Msg.ShowInfo(Player.GetProfiles()),
        ["show-properties"] = args => Player.Command("script-binding select/show-properties"),
        ["show-protocols"] = args => ShowProtocols(),
        ["window-scale"] = WindowScaleCommand,


        // deprecated
        ["show-recent"] = args => ShowRemoved(), // deprecated
        ["quick-bookmark"] = args => QuickBookmark(), // deprecated
        ["show-history"] = args => ShowHistory(), // deprecated
        ["show-playlist"] = args => Player.Command("script-binding select/select-playlist"), // deprecated
        ["show-command-palette"] = args => Player.Command("script-binding select/select-binding"), // deprecated
        ["show-audio-tracks"] = args => Player.Command("script-binding select/select-aid"), // deprecated
        ["show-subtitle-tracks"] = args => Player.Command("script-binding select/select-sid"), // deprecated
        ["show-audio-devices"] = args => Player.Command("script-binding select/select-audio-device"), // deprecated
    };

    void MoveWindowCommand(IList<string> args)
    {
        if (!GuiCommandArgumentParser.TryGetRequired(args, "move-window", out string direction))
            return;

        MoveWindow?.Invoke(direction);
    }

    void ScaleWindowCommand(IList<string> args)
    {
        if (!GuiCommandArgumentParser.TryGetInvariantFloat(args, "scale-window", out float scale))
            return;

        ScaleWindow?.Invoke(scale);
    }

    void WindowScaleCommand(IList<string> args)
    {
        if (!GuiCommandArgumentParser.TryGetInvariantFloat(args, "window-scale", out float scale))
            return;

        WindowScaleNet?.Invoke(scale);
    }

    void ShowDialog(Type winType)
    {
        if (Activator.CreateInstance(winType) is not Window window)
            throw new InvalidOperationException($"Could not create WPF window: {winType.FullName}");

        if (MainForm.Instance is { } mainForm)
            new WindowInteropHelper(window).Owner = mainForm.Handle;

        window.ShowDialog();
    }

    void LoadSubtitle(IList<string> args)
    {
        using var dialog = new OpenFileDialog();
        string path = Player.GetPropertyString("path");

        if (File.Exists(path))
            dialog.InitialDirectory = Path.GetDirectoryName(path);

        dialog.Multiselect = true;

        if (dialog.ShowDialog() == DialogResult.OK)
            foreach (string filename in dialog.FileNames)
                Player.CommandV("sub-add", filename);
    }

    void OpenFiles(IList<string> args)
    {
        bool append = false;

        foreach (string arg in args)
            if (arg == "append")
                append = true;

        using var dialog = new OpenFileDialog()
        {
            Filter = FileTypes.GetOpenFileDialogFilter(),
            Multiselect = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            Player.LoadFiles(dialog.FileNames, true, append);
    }

    void Open_DVD_Or_BD_Folder(IList<string> args)
    {
        using var dialog = new FolderBrowserDialog();

        if (dialog.ShowDialog() == DialogResult.OK)
            Player.LoadDiskFolder(dialog.SelectedPath);
    }

    void EditCongFile(IList<string> args)
    {
        if (!GuiCommandArgumentParser.TryGetRequired(args, "edit-conf-file", out string configFile))
            return;

        string file = Player.ConfigFolder + configFile;

        if (!File.Exists(file))
        {
            string msg = string.Format(
                _("{0} does not exist. Would you like to create it?"),
                configFile);

            if (Msg.ShowQuestion(msg) == MessageBoxResult.OK)
                FileHelp.WriteAllTextAtomic(file, "");
        }
        
        if (File.Exists(file))
            ProcessHelp.ShellExecute(WinApiHelp.GetAppPathForExtension("txt"), "\"" + file + "\"");
    }

    void ShowTextWithEditor(string name, string text)
    {
        string file = Path.Combine(AppPaths.Temp, name + ".txt");
        App.TempFiles.Add(file);
        File.WriteAllText(file, BR + text.Trim() + BR);
        ProcessHelp.ShellExecute(WinApiHelp.GetAppPathForExtension("txt"), "\"" + file + "\"");
    }

    void ShowCommands()
    {
        string json = Player.GetPropertyString("command-list");
        var enumerator = JsonDocument.Parse(json).RootElement.EnumerateArray();
        var commands = enumerator.OrderBy(it => it.GetProperty("name").GetString());
        StringBuilder sb = new StringBuilder();

        foreach (var cmd in commands)
        {
            sb.AppendLine();
            sb.AppendLine(cmd.GetProperty("name").GetString());

            foreach (var args in cmd.GetProperty("args").EnumerateArray())
            {
                string argumentName = args.GetProperty("name").GetString() ?? "";
                string argumentType = args.GetProperty("type").GetString() ?? "";
                string value = argumentName + " <" + argumentType.ToLowerInvariant() + ">";

                if (args.GetProperty("optional").GetBoolean())
                    value = "[" + value + "]";

                sb.AppendLine("    " + value);
            }
        }

        string header = BR +
            "https://mpv.io/manual/master/#list-of-input-commands" + BR;

        ShowTextWithEditor("Input Commands", header + sb.ToString());
    }

    void ShowKeys() =>
        ShowTextWithEditor("Keys", Player.GetPropertyString("input-key-list").Replace(",", BR));

    void ShowProtocols() =>
        ShowTextWithEditor("Protocols", Player.GetPropertyString("protocol-list").Replace(",", BR));

    void ShowDecoders() =>
        ShowTextWithEditor("Decoders", Player.GetPropertyOsdString("decoder-list").Replace(",", BR));

    void ShowDemuxers() =>
        ShowTextWithEditor("Demuxers", Player.GetPropertyOsdString("demuxer-lavf-list").Replace(",", BR));

    void OpenFromClipboard(IList<string> args)
    {
        bool append = args.Count == 1 && args[0] == "append";

        if (System.Windows.Forms.Clipboard.ContainsFileDropList())
        {
            string[] files = System.Windows.Forms.Clipboard.GetFileDropList().Cast<string>().ToArray();
            Player.LoadFiles(files, false, append);

            if (append)
                Player.CommandV("show-text", _("Files/URLs were added to the playlist"));
        }
        else
        {
            string clipboard = System.Windows.Forms.Clipboard.GetText();
            List<string> files = [];

            foreach (string i in clipboard.Split(BR.ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                if (FileTypes.IsStreamingUrl(i) || File.Exists(i) || FileTypes.IsSupportedMediaInput(i))
                    files.Add(i);
            }

            if (files.Count == 0)
            {
                Terminal.WriteError(_("The clipboard does not contain a valid URL or file."));
                return;
            }

            Player.LoadFiles(files.ToArray(), false, append);

            if (append)
                Player.CommandV("show-text", _("Files/URLs were added to the playlist"));
        }
    }

    void LoadAudio(IList<string> args)
    {
        using var dialog = new OpenFileDialog();
        string path = Player.GetPropertyString("path");

        if (File.Exists(path))
            dialog.InitialDirectory = Path.GetDirectoryName(path);

        dialog.Multiselect = true;

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        foreach (string i in dialog.FileNames)
        {
            Player.CommandV("audio-add", i);
        }
    }

    void RegisterFileAssociations(IList<string> args)
    {
        if (!GuiCommandArgumentParser.TryGetRequired(args, "reg-file-assoc", out string perceivedType))
            return;

        string[] extensions = Windows.FileAssociation.GetExtensionsForPerceivedType(perceivedType);

        try
        {
            int exitCode = FileAssociationRegistrar.RegisterElevated(perceivedType, extensions);

            if (exitCode == 0)
            {
                string msgRestart = _("File Explorer icons will refresh after process restart.");

                if (perceivedType == "unreg")
                    Msg.ShowInfo(_("File associations were successfully removed.") + BR2 + msgRestart);
                else
                    Msg.ShowInfo(_("File associations were successfully created.") + BR2 + msgRestart);
            }
            else
                Msg.ShowError(_("Error creating file associations."));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to register file associations.");
        }
    }

    void ShowMediaInfo(IList<string> args)
    {
        if (Player.PlaylistPos == -1)
            return;

        bool full = args.Contains("full");
        bool raw = args.Contains("raw");
        bool editor = args.Contains("editor");
        bool osd = args.Contains("osd") || args.Count == 0;
        string path = Player.GetPropertyString("path");

        if (TryShowSimpleOsdMediaInfo(path, osd) || TryShowStreamMediaInfo(path))
            return;

        string text = GetDetailedMediaInfo(path, full, raw, osd).TrimEx();
        ShowMediaInfoText(text, editor, osd);
    }

    bool TryShowSimpleOsdMediaInfo(string path, bool osd)
    {
        if (!osd || !File.Exists(path))
            return false;

        if (FileTypes.IsAudio(path.Ext()))
        {
            Player.CommandV("show-text", Player.GetPropertyOsdString("filtered-metadata"), "5000");
            return true;
        }

        if (!FileTypes.IsImage(path.Ext()))
            return false;

        long fileSize = new FileInfo(path).Length;
        string text =
            _("Width") + ": " + Player.GetPropertyInt("width") + "\n" +
            _("Height") + ": " + Player.GetPropertyInt("height") + "\n" +
            _("Size") + ": " + Convert.ToInt32(fileSize / 1024.0) + " KB\n" +
            _("Type") + ": " + path.Ext().ToUpper();

        Player.CommandV("show-text", text, "5000");
        return true;
    }

    bool TryShowStreamMediaInfo(string path)
    {
        if (!path.Contains("://"))
            return false;

        string mediaTitle = Player.GetPropertyString("media-title");
        string videoFormat = Player.GetPropertyString("video-format").ToUpper();
        string audioCodec = Player.GetPropertyString("audio-codec-name").ToUpper();
        int width = Player.GetPropertyInt("video-params/w");
        int height = Player.GetPropertyInt("video-params/h");
        TimeSpan duration = TimeSpan.FromSeconds(Player.GetPropertyDouble("duration"));
        string text =
            mediaTitle.FileName() + "\n" +
            FormatTime(duration.TotalMinutes) + ":" + FormatTime(duration.Seconds) + "\n" +
            $"{width} x {height}\n" +
            $"{videoFormat}\n{audioCodec}";

        Player.CommandV("show-text", text, "5000");
        return true;
    }

    string GetDetailedMediaInfo(string path, bool full, bool raw, bool osd)
    {
        if (App.MediaInfo && !osd && File.Exists(path) && !path.Contains(@"\\.\pipe\"))
        {
            using MediaInfo mediaInfo = new(path);
            return Regex.Replace(mediaInfo.GetSummary(full, raw), "Unique ID.+", "");
        }

        Player.UpdateExternalTracks();
        StringBuilder text = new("N: " + Player.GetPropertyString("filename") + BR);

        lock (Player.MediaTracksLock)
        {
            foreach (MediaTrack track in Player.MediaTracks)
                text.Append(track.Text).Append(BR);
        }

        return text.ToString();
    }

    void ShowMediaInfoText(string text, bool editor, bool osd)
    {
        if (editor)
            ShowTextWithEditor("media-info", text);
        else if (osd)
            Command.ShowText(text.Replace("\r", ""), 5000, 16);
        else
        {
            MessageBoxEx.SetFont("Consolas");
            Msg.ShowInfo(text);
            MessageBoxEx.SetFont("Segoe UI");
        }
    }

    string FormatTime(double value) => ((int)value).ToString("00");

    void ShowBindings() => ShowTextWithEditor(_("Bindings"), Player.UsedInputConfContent);

    void AddToPath()
    {
        string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";

        if (path.Contains(AppPaths.Startup.TrimEnd(Path.DirectorySeparatorChar), StringComparison.CurrentCultureIgnoreCase))
        {
            Msg.ShowWarning(_("mpv.net is already in the Path environment variable."));
            return;
        }

        Environment.SetEnvironmentVariable("Path",
            AppPaths.Startup.TrimEnd(Path.DirectorySeparatorChar) + ";" + path,
            EnvironmentVariableTarget.User);

        Msg.ShowInfo(_("mpv.net was successfully added to the Path environment variable."));
    }

    void RemoveFromPath()
    {
        string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";

        if (!path.Contains(AppPaths.Startup.TrimEnd(Path.DirectorySeparatorChar)))
        {
            Msg.ShowWarning(_("mpv.net was not found in the Path environment variable."));
            return;
        }

        path = path.Replace(AppPaths.Startup.TrimEnd(Path.DirectorySeparatorChar), "");
        path = path.Replace(";;", ";").Trim(';');

        Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.User);

        Msg.ShowInfo(_("mpv.net was successfully removed from the Path environment variable."));
    }

    // deprecated
    void QuickBookmark() =>
        Msg.ShowInfo(_("This feature was removed, but there are user scripts:") + BR2 +
            "https://github.com/stax76/mpv-scripts/blob/main/misc.lua");

    // deprecated
    void ShowHistory() =>
        Msg.ShowInfo(_("This feature was removed, but there are user scripts:") + BR2 +
            "https://github.com/stax76/mpv-scripts/blob/main/history.lua");

    // deprecated
    void ShowRemoved() => Msg.ShowInfo(_("This feature was removed."));
}
