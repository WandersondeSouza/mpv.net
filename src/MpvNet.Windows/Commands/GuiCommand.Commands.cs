namespace MpvNet;

using MpvNet.Windows.WPF;
using MpvNet.Windows.WPF.Views;

public partial class GuiCommand
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
        ["show-recent"] = args => ShowRemoved(),
        ["quick-bookmark"] = args => QuickBookmark(),
        ["show-history"] = args => ShowHistory(),
        ["show-playlist"] = args => Player.Command("script-binding select/select-playlist"),
        ["show-command-palette"] = args => Player.Command("script-binding select/select-binding"),
        ["show-audio-tracks"] = args => Player.Command("script-binding select/select-aid"),
        ["show-subtitle-tracks"] = args => Player.Command("script-binding select/select-sid"),
        ["show-audio-devices"] = args => Player.Command("script-binding select/select-audio-device"),
    };
}
