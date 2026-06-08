
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
    public string DonationCopied { get; } = AppClass.DonationCopied;
    public string DonationPixKey { get; } = "00020126490014BR.GOV.BCB.PIX0127wanderson_souza@hotmail.com5204000053039865802BR5901N6001C62070503***630410BE";

    [RelayCommand]
    public void Close() => CloseAction!();
}
