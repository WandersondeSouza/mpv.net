
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.Input;

using MpvNet.Help;
using MpvNet.Windows.UI;
using MpvNet.Windows.WPF.Controls;
using MpvNet.Windows.WPF.ViewModels;

namespace MpvNet.Windows.WPF;

public partial class ConfWindow : Window, INotifyPropertyChanged
{
    List<Setting> _settings = Conf.LoadConf(Properties.Resources.editor_conf.TrimEnd());
    List<ConfItem> _confItems = new List<ConfItem>();
    string _initialContent;
    string _themeConf = GetThemeConf();
    string? _searchText;
    List<NodeViewModel>? _nodes;
    bool _shown;
    int _useSpace;
    int _useNoSpace;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ConfWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadConf(Player.ConfPath);
        LoadConf(App.ConfPath);
        LoadLibplaceboConf();
        LoadSettings();
        _initialContent = GetCompareString();

        if (string.IsNullOrEmpty(App.Settings.ConfigEditorSearch))
            SearchText = "General:";
        else
            SearchText = App.Settings.ConfigEditorSearch;

        foreach (var node in Nodes)
            SelectNodeFromSearchText(node);

        foreach (var node in Nodes)
            node.IsExpanded = true;
    }

    public ObservableCollection<string> FilterStrings { get; } = new();

    public Theme? Theme => Theme.Current;

    public string SearchText
    {
        get => _searchText ?? "";
        set
        {
            _searchText = value;
            SearchTextChanged();
            OnPropertyChanged();
        }
    }

    public List<NodeViewModel> Nodes
    {
        get
        {
            if (_nodes == null)
            {
                var rootNode = new TreeNode();

                foreach (Setting setting in _settings)
                    AddNode(rootNode.Children, setting.Directory!);

                _nodes = new NodeViewModel(rootNode).Children;
            }

            return _nodes;
        }
    }

    public static TreeNode? AddNode(IList<TreeNode> nodes, string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        TreeNode? currentNode = null;

        foreach (string part in parts)
        {
            currentNode = nodes.FirstOrDefault(node => node.Name == part);

            if (currentNode == null)
            {
                currentNode = new TreeNode() { Name = part };
                nodes.Add(currentNode);
            }

            nodes = currentNode.Children;
        }

        return currentNode;
    }

    void LoadSettings()
    {
        foreach (Setting setting in _settings)
        {
            setting.StartValue = setting.Value;

            if (!FilterStrings.Contains(setting.Directory!))
                FilterStrings.Add(setting.Directory!);

            foreach (ConfItem item in _confItems)
            {
                if (setting.Name == item.Name &&
                    setting.File == item.File &&
                    item.Section == "" && !item.IsSectionItem)
                {
                    setting.Value = item.Value;
                    setting.PersistedValue = item.Value;

                    if (setting.Name == "language" && item.Value == "system")
                    {
                        item.Value = App.Language;
                        setting.Value = item.Value;
                        setting.PersistedValue = item.Value;
                    }

                    setting.StartValue = setting.Value;
                    setting.ConfItem = item;
                    item.SettingBase = setting;
                }
            }

            if (setting.Name == "language" && setting.Value == "system")
            {
                setting.PersistedValue = App.Language;
                setting.Value = App.Language;
                setting.StartValue = setting.Value;
            }
            else if (setting.Name == "language" && setting.ConfItem == null)
            {
                setting.PersistedValue = App.Language;
                setting.Value = App.Language;
                setting.StartValue = setting.Value;
            }

            switch (setting)
            {
                case StringSetting s:
                    MainStackPanel.Children.Add(new StringSettingControl(s) { Visibility = Visibility.Collapsed });
                    break;
                case OptionSetting s:
                    if (s.Options.Count > 3)
                        MainStackPanel.Children.Add(new ComboBoxSettingControl(s) { Visibility = Visibility.Collapsed });
                    else
                        MainStackPanel.Children.Add(new OptionSettingControl(s) { Visibility = Visibility.Collapsed });
                    break;
            }
        }
    }

    static string GetThemeConf() => Theme.DarkMode + App.DarkTheme + App.LightTheme;

    string GetCompareString() => string.Join("", _settings.Select(item => item.Name + item.Value).ToArray());

    void LoadConf(string file)
    {
        if (!File.Exists(file))
            return;

        string comment = "";
        string section = "";

        bool isSectionItem = false;

        foreach (string it in File.ReadAllLines(file))
        {
            string line = it.Trim();

            if (line.StartsWith("-"))
                line = line.TrimStart('-');

            if (line == "")
                comment += "\r\n";
            else if (line.StartsWith("#"))
                comment += line.Trim() + "\r\n";
            else if (line.StartsWith("[") && line.Contains(']'))
            {
                if (!isSectionItem && comment != "" && comment != "\r\n")
                    _confItems.Add(new ConfItem() {
                        Comment = comment, File = Path.GetFileNameWithoutExtension(file)});

                section = line.Substring(0, line.IndexOf("]") + 1);
                comment = "";
                isSectionItem = true;
            }
            else if (IsConfigValueLine(line))
            {
                ConfItem item = CreateConfItem(line, file, comment, section, isSectionItem);
                comment = "";
                section = "";
                _confItems.Add(item);
            }
        }
    }

    bool IsConfigValueLine(string line) =>
        line.Contains('=') || Regex.Match(line, "^[\\w-]+$").Success;

    ConfItem CreateConfItem(string line, string file, string comment, string section, bool isSectionItem)
    {
        line = EnsureExplicitValue(line);

        if (line.Contains(" =") || line.Contains("= "))
            _useSpace += 1;
        else
            _useNoSpace += 1;

        ConfItem item = new()
        {
            File = Path.GetFileNameWithoutExtension(file),
            IsSectionItem = isSectionItem,
            Comment = comment,
            Section = section
        };

        line = ExtractLineComment(line, item);

        int pos = line.IndexOf("=");
        string left = line.Substring(0, pos).Trim().ToLower().TrimStart('-');
        string right = UnquoteValue(line.Substring(pos + 1).Trim());

        item.Name = NormalizeSettingName(left);
        item.Value = right;
        return item;
    }

    static string EnsureExplicitValue(string line)
    {
        if (line.Contains('='))
            return line;

        if (line.StartsWith("no-"))
            return line.Substring(3) + "=no";

        return line + "=yes";
    }

    static string ExtractLineComment(string line, ConfItem item)
    {
        if (line.Contains('#') && !line.Contains('\'') && !line.Contains('"'))
        {
            item.LineComment = line.Substring(line.IndexOf("#")).Trim();
            return line.Substring(0, line.IndexOf("#")).Trim();
        }

        return line;
    }

    static string UnquoteValue(string value)
    {
        if (value.StartsWith('\'') && value.EndsWith('\''))
            return value.Trim('\'');

        if (value.StartsWith('"') && value.EndsWith('"'))
            return value.Trim('"');

        return value;
    }

    static string NormalizeSettingName(string name) =>
        name switch
        {
            "fs" => "fullscreen",
            "loop" => "loop-file",
            _ => name
        };

    string GetKeyValueContent(string filename)
    {
        List<string> pairs = new();

        foreach (Setting setting in _settings)
        {
            if (filename != setting.File)
                continue;

            string value = GetValueToSave(setting);

            if (value != setting.Default)
                pairs.Add(setting.Name + "=" + EscapeValue(value));
        }

        return string.Join(',', pairs);
    }

    void LoadLibplaceboConf()
    {
        foreach (ConfItem item in _confItems.ToArray())
            if (item.Name == "libplacebo-opts")
                LoadKeyValueList(item.Value, "libplacebo");
    }

    void LoadKeyValueList(string options, string file)
    {
        string[] optionStrings = options.Split(",", StringSplitOptions.RemoveEmptyEntries);

        foreach (string pair in optionStrings)
        {
            if (!pair.Contains('='))
                continue;

            int pos = pair.IndexOf("=");
            string left = pair.Substring(0, pos).Trim().ToLower();
            string right = pair.Substring(pos + 1).Trim();

            ConfItem item = new();
            item.Name = left;
            item.Value = right;
            item.File = file;
            _confItems.Add(item);
        }
    }

    string EscapeValue(string value)
    {
        if (value.Contains('\''))
            return '"' + value + '"';

        if (value.Contains('"'))
            return '\'' + value + '\'';

        if (value.Contains('"') || value.Contains('#') || value.StartsWith("%") ||
            value.StartsWith(" ") || value.EndsWith(" "))
        {
            return '\'' + value + '\'';
        }

        return value;
    }

    string GetContent(string filename)
    {
        StringBuilder sb = new StringBuilder();
        List<string> namesWritten = new List<string>();
        string equalString = _useSpace > _useNoSpace ? " = " : "=";

        foreach (ConfItem item in _confItems)
        {
            if (filename != item.File || item.Section != "" || item.IsSectionItem)
                continue;

            if (item.Comment != "")
                sb.Append(item.Comment);

            if (item.SettingBase == null)
            {
                if (item.Name != "")
                {
                    sb.Append(item.Name + equalString + EscapeValue(item.Value));

                    if (item.LineComment != "")
                        sb.Append(" " + item.LineComment);

                    sb.AppendLine();
                    namesWritten.Add(item.Name);
                }
            }
            else if (GetValueToSave(item.SettingBase) != item.SettingBase.Default)
            {
                sb.Append(item.Name + equalString + EscapeValue(GetValueToSave(item.SettingBase)));

                if (item.LineComment != "")
                    sb.Append(" " + item.LineComment);

                sb.AppendLine();
                namesWritten.Add(item.Name);
            }
        }

        foreach (Setting setting in _settings)
        {
            if (filename != setting.File || namesWritten.Contains(setting.Name!))
                continue;

            string value = GetValueToSave(setting);

            if (value != setting.Default)
                sb.AppendLine(setting.Name + equalString + EscapeValue(value));
        }

        foreach (ConfItem item in _confItems)
        {
            if (filename != item.File || (item.Section == "" && !item.IsSectionItem))
                continue;

            if (item.Section != "")
            {
                if (!sb.ToString().EndsWith("\r\n\r\n"))
                    sb.AppendLine();

                sb.AppendLine(item.Section);
            }

            if (item.Comment != "")
                sb.Append(item.Comment);

            sb.Append(item.Name + equalString + EscapeValue(item.Value));

            if (item.LineComment != "")
                sb.Append(" " + item.LineComment);

            sb.AppendLine();
            namesWritten.Add(item.Name);
        }

        return "\r\n" + sb.ToString().Trim() + "\r\n";
    }

    void SearchTextChanged()
    {
        string activeFilter = "";

        foreach (string i in FilterStrings)
            if (SearchText == i + ":")
                activeFilter = i;

        if (activeFilter == "")
        {
            foreach (UIElement i in MainStackPanel.Children)
                if ((i as ISettingControl)!.Contains(SearchText) && SearchText.Length > 1)
                    i.Visibility = Visibility.Visible;
                else
                    i.Visibility = Visibility.Collapsed;

            foreach (var node in Nodes)
                UnselectNode(node);
        }
        else
            foreach (UIElement i in MainStackPanel.Children)
                if ((i as ISettingControl)!.Setting.Directory == activeFilter)
                    i.Visibility = Visibility.Visible;
                else
                    i.Visibility = Visibility.Collapsed;

        MainScrollViewer.ScrollToTop();
    }
    
    void ConfWindow1_Loaded(object sender, RoutedEventArgs e)
    {
        SearchControl.SearchTextBox.SelectAll();
        Keyboard.Focus(SearchControl.SearchTextBox);

        foreach (var i in MainStackPanel.Children.OfType<StringSettingControl>())
            i.Update();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        App.Settings.ConfigEditorSearch = SearchText;

        if (_initialContent == GetCompareString())
            return;

        foreach (Setting setting in _settings)
        {
            if (setting.Name == "libplacebo-opts")
            {
                setting.Value = GetKeyValueContent("libplacebo");
                break;
            }
        }

        FileHelp.WriteAllTextAtomic(Player.ConfPath, GetContent("mpv"));
        FileHelp.WriteAllTextAtomic(App.ConfPath, GetContent("mpvnet"));

        foreach (Setting it in _settings)
        {
            if (it.Value != it.StartValue)
            {
                if (it.File == "mpv")
                {
                    Player.ProcessProperty(it.Name, it.Value);
                    Player.SetPropertyString(it.Name!, it.Value!);
                }
                else if (it.File == "mpvnet")
                    App.ProcessProperty(it.Name ?? "", it.Value ?? "", true);
            }
        }

        Theme.Init();
        Theme.UpdateWpfColors();

        if (_themeConf != GetThemeConf())
            MessageBox.Show(_("Changed theme settings require mpv.net being restarted."), _("Info"));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == Key.Escape)
            Close();

        if (e.Key == Key.F3 || e.Key == Key.F6 || (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control))
        {
            Keyboard.Focus(SearchControl.SearchTextBox);
            SearchControl.SearchTextBox.SelectAll();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    string GetValueToSave(Setting setting)
    {
        if (setting.Name == "language" &&
            setting.PersistedValue == "system" &&
            setting.Value == setting.StartValue)
        {
            return "system";
        }

        return setting.Value ?? "";
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        if (_shown)
            return;

        _shown = true;

        Application.Current.Dispatcher.BeginInvoke(() => {
            SearchControl.SearchTextBox.SelectAll();
        },
        DispatcherPriority.Background);
    }

    void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var node = TreeView.SelectedItem as NodeViewModel;

        if (node == null)
            return;

        Application.Current.Dispatcher.BeginInvoke(() => {
            SearchText = node!.Path + ":";
        },
        DispatcherPriority.Background);
    }

    void MenuTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        MainContextMenu.PlacementTarget = MenuTextBlock;
        MainContextMenu.Placement = PlacementMode.Top;
        MainContextMenu.IsOpen = true;
        e.Handled = true;
    }

    void SelectNodeFromSearchText(NodeViewModel node)
    {
        if (node.Path + ":" == SearchText)
        {
            node.IsSelected = true;
            node.IsExpanded = true;
            return;
        }

        foreach (var it in node.Children)
            SelectNodeFromSearchText(it);
    }

    void UnselectNode(NodeViewModel node)
    {
        if (node.IsSelected)
            node.IsSelected = false;

        foreach (var it in node.Children)
            UnselectNode(it);
    }

    [RelayCommand] void ShowMpvNetSpecificSettings() => SearchText = "mpv.net";

    [RelayCommand] void PreviewMpvConfFile() => Msg.ShowInfo(GetContent("mpv"));
    
    [RelayCommand] void PreviewMpvNetConfFile() => Msg.ShowInfo(GetContent("mpvnet"));

    [RelayCommand] void ShowMpvManual() => ProcessHelp.ShellExecute("https://mpv.io/manual/master/");
    
    [RelayCommand] void ShowMpvNetManual() => ProcessHelp.ShellExecute("https://github.com/WandersondeSouza/mpv.net/blob/main/docs/manual.md");

    public string WindowTitle => _("Config Editor");
    public string SearchHintText => _("Find a setting (Ctrl+F)");
    public string MenuText => _("Menu");
    public string ShowMpvNetOptionsText => _("Show mpv.net options");
    public string PreviewMpvConfText => _("Preview mpv.conf");
    public string PreviewMpvNetConfText => _("Preview mpvnet.conf");
    public string ShowMpvManualText => _("Show mpv manual");
    public string ShowMpvNetManualText => _("Show mpv.net manual");
}
