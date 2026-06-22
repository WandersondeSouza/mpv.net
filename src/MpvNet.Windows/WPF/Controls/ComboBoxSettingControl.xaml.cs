
using System.Windows;
using System.Windows.Controls;

using MpvNet.Windows.UI;
using MpvNet.Windows.WinForms;

namespace MpvNet.Windows.WPF;

public partial class ComboBoxSettingControl : UserControl, ISettingControl
{
    readonly OptionSetting _optionSetting;

    public ComboBoxSettingControl(OptionSetting optionSetting)
    {
        _optionSetting = optionSetting;
        InitializeComponent();
        DataContext = this;
        TitleTextBox.Text = optionSetting.DisplayName ?? optionSetting.Name;

        if (string.IsNullOrEmpty(optionSetting.Help))
            HelpTextBox.Visibility = Visibility.Collapsed;

        HelpTextBox.Text = optionSetting.Help;
        ComboBoxControl.ItemsSource = optionSetting.Options;

        foreach (var item in optionSetting.Options)
            if (item.Name == optionSetting.Value)
                ComboBoxControl.SelectedItem = item;

        if (string.IsNullOrEmpty(optionSetting.URL))
            LinkTextBlock.Visibility = Visibility.Collapsed;

        Link.SetURL(optionSetting.URL);
    }

    public Theme? Theme => Theme.Current;

    public Setting Setting => _optionSetting;

    public bool Contains(string searchString) => ContainsInternal(searchString.ToLower());

    public bool ContainsInternal(string search)
    {
        if (TitleTextBox.Text.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) > -1)
            return true;

        if (HelpTextBox.Text.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) > -1)
            return true;

        foreach (var option in _optionSetting.Options)
        {
            if (option.Text?.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) > -1)
                return true;

            if (option.Help?.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) > -1)
                return true;

            if (option.Name?.IndexOf(search, StringComparison.InvariantCultureIgnoreCase) > -1)
                return true;
        }

        return false;
    }

    void ComboBoxControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _optionSetting.Value = (ComboBoxControl.SelectedItem as OptionSettingOption)?.Name;

        if (_optionSetting.Name == "language")
        {
            App.Language = _optionSetting.Value ?? "";
            TranslationProvider.Current?.Gettext("");
            MainForm.Instance?.RebuildContextMenu();
        }
    }
}
