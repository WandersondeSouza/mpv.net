using System.Drawing;
using System.Windows.Forms;

using static MpvNet.Windows.Native.WinApi;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    void PropChangeFullscreen(bool value) => BeginInvoke(() => CycleFullscreen(value));

    bool IsFullscreen => WindowState == FormWindowState.Maximized && FormBorderStyle == FormBorderStyle.None;

    public void CycleFullscreen(bool enabled)
    {
        _lastCycleFullscreen = Environment.TickCount;
        Player.Fullscreen = enabled;

        if (enabled)
        {
            if (WindowState != FormWindowState.Maximized || FormBorderStyle != FormBorderStyle.None)
            {
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;

                if (_wasMaximized)
                {
                    Rectangle bounds = Screen.FromControl(this).Bounds;
                    uint SWP_SHOWWINDOW = 0x0040;
                    IntPtr HWND_TOP= IntPtr.Zero;
                    SetWindowPos(Handle, HWND_TOP, bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_SHOWWINDOW);
                }
            }
        }
        else
        {
            if (WindowState == FormWindowState.Maximized && FormBorderStyle == FormBorderStyle.None)
            {
                if (_wasMaximized)
                    WindowState = FormWindowState.Maximized;
                else
                {
                    WindowState = FormWindowState.Normal;

                    if (!Player.WasInitialSizeSet)
                        SetFormPosAndSize();
                }

                FormBorderStyle = Player.Border ? FormBorderStyle.Sizable : FormBorderStyle.None;

                if (!KeepSize())
                    SetFormPosAndSize();
            }
        }
    }
}
