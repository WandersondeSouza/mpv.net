using MpvNet.Extensions;

using WpfControls = System.Windows.Controls;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    static string GetLocalizedMenuPath(Binding binding, IReadOnlyDictionary<string, string> defaultMenuLabels)
    {
        if (binding.Command != "" && defaultMenuLabels.TryGetValue(binding.Command, out string? defaultLabel))
            return defaultLabel;

        return TranslateMenuPath(binding.Comment);
    }

    static string TranslateMenuPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        string[] parts = path.Split(new[] { " > ", " | " }, StringSplitOptions.None);

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "-" || parts[i] == "")
                continue;

            parts[i] = _(parts[i]);
        }

        return string.Join(" > ", parts);
    }

    void AddTrackMenuItems(WpfControls.MenuItem parent, IEnumerable<MediaTrack> tracks, string propertyName, string selectedId)
    {
        foreach (MediaTrack track in tracks)
        {
            var menuItem = CreateTrackMenuItem(track);
            menuItem.Click += (sender, args) => Player.CommandV("set", propertyName, track.ID.ToString());
            menuItem.IsChecked = selectedId == track.ID.ToString();
            parent.Items.Add(menuItem);
        }
    }

    void AddNoSubtitlesMenuItem(WpfControls.MenuItem parent)
    {
        var menuItem = new WpfControls.MenuItem() { Header = "S: " + _("No subtitles") };
        menuItem.Click += (sender, args) => Player.CommandV("set", "sid", "no");
        menuItem.IsChecked = Player.SID == "no";
        parent.Items.Add(menuItem);
    }

    void AddEditionMenuItems(WpfControls.MenuItem parent, IEnumerable<MediaTrack> tracks)
    {
        foreach (MediaTrack track in tracks)
        {
            var menuItem = CreateTrackMenuItem(track);
            menuItem.Click += (sender, args) => Player.CommandV("set", "edition", track.ID.ToString());
            menuItem.IsChecked = Player.Edition == track.ID;
            parent.Items.Add(menuItem);
        }
    }

    static WpfControls.MenuItem CreateTrackMenuItem(MediaTrack track) =>
        new() { Header = track.Text.Replace("_", "__") };

    public WpfControls.MenuItem? FindMenuItem(string text, string text2 = "") {
        var ret = FindMenuItem(text, ContextMenu.Items);

        if (ret == null && text2 != "")
            return FindMenuItem(text2, ContextMenu.Items);

        return ret;
    }

    WpfControls.MenuItem? FindMenuItem(string text, WpfControls.ItemCollection? items)
    {
        foreach (object item in items!)
        {
            if (item is WpfControls.MenuItem mi)
            {
                if (mi.Header.ToString().StartsWithEx(text) && mi.Header.ToString().TrimEx() == text)
                    return mi;

                if (mi.Items.Count > 0)
                {
                    WpfControls.MenuItem? val = FindMenuItem(text, mi.Items);

                    if (val != null)
                        return val;
                }
            }
        }

        return null;
    }
}
