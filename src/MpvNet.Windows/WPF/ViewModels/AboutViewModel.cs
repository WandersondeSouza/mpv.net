
using CommunityToolkit.Mvvm.Input;

namespace MpvNet.Windows.WPF.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    public Action? CloseAction { get; set; }

    public string WindowTitle { get; } = _("About") + " " + AppInfo.Product;
    public string About { get; } = AppClass.About;
    public string CodecGuideLabel { get; } = _("Codec Guide: ");
    public string DonationTitle { get; } = AppClass.DonationTitle;
    public string DonationCopyPaste { get; } = AppClass.DonationCopyPaste;

    [RelayCommand]
    public void Close() => CloseAction!();
}
