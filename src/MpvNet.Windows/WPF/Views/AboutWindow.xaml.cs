
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;

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

    void DonationPixKey_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AboutViewModel vm)
            return;

        System.Windows.Clipboard.SetText(vm.DonationPixKey);
        Msg.ShowInfo(vm.DonationCopied);
    }
}
