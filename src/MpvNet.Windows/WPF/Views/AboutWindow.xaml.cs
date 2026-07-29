
using System.Windows;
using System.Windows.Documents;

using MpvNet.Help;
using MpvNet.Windows.WPF.ViewModels;

namespace MpvNet.Windows.WPF.Views;

public partial class AboutWindow
{

    public AboutWindow()
    {
        InitializeComponent();
        var vm = new AboutViewModel();
        DataContext = vm;
        vm.CloseAction = Close;
    }

    void CodecGuideLink_Click(object sender, RoutedEventArgs e) =>
        ProcessHelp.ShellExecute("https://codecguide.com/");

    void RepositoryLink_Click(object sender, RoutedEventArgs e) =>
        ProcessHelp.ShellExecute("https://github.com/WandersondeSouza/mpv.net");

    void DonationLink_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AboutViewModel vm)
            ProcessHelp.ShellExecute(vm.DonationUrl);
    }
}
