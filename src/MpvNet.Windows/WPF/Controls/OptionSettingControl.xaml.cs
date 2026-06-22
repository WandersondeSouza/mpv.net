
using System.Windows;
using System.Windows.Controls;

using MpvNet.Windows.UI;

namespace MpvNet.Windows.WPF;

public partial class OptionSettingControl : UserControl, ISettingControl
{
    readonly OptionSetting _optionSetting;

    public OptionSettingControl(OptionSetting optionSetting)
    {
        _optionSetting = optionSetting;
        InitializeComponent();
        DataContext = this;
        TitleTextBox.Text = optionSetting.DisplayName ?? optionSetting.Name;

        if (string.IsNullOrEmpty(optionSetting.Help))
            HelpTextBox.Visibility = Visibility.Collapsed;

        HelpTextBox.Text = optionSetting.Help;

        if (string.IsNullOrEmpty(optionSetting.Help))
            LinkTextBlock.Margin = new Thickness(2, 6, 0, 0);

        ItemsControl.ItemsSource = optionSetting.Options;

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
}
