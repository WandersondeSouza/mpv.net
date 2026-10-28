namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    TitleBarFullscreenButton? _titleBarFullscreenButton;
    bool _titleBarActive = true;

    void UpdateTitleBarFullscreenButton()
    {
        if (_managedResourcesDisposed || !WasShown)
            return;

        _titleBarFullscreenButton ??= new TitleBarFullscreenButton(this, () =>
        {
            if (!_managedResourcesDisposed && !_closingInput && Player.Border && Player.TitleBar && !Player.Fullscreen)
                Player.Command("cycle fullscreen");
        });
        _titleBarFullscreenButton.UpdateCaption(
            !_closingInput && Player.Border && Player.TitleBar && !Player.Fullscreen && !IsFullscreen,
            UI.Theme.DarkMode, _titleBarActive, _("Fullscreen"));
    }
}
