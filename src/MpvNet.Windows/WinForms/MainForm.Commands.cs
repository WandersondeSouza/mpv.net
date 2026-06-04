using System.Globalization;
using System.Drawing;
using System.Windows.Forms;

using static MpvNet.Windows.Help.WinApiHelp;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    void GuiCommand_ScaleWindow(float scale)
    {
        BeginInvoke(() => {
            int w, h;

            if (KeepSize())
            {
                w = (int)(ClientSize.Width * scale);
                h = (int)(ClientSize.Height * scale);
            }
            else
            {
                w = (int)(ClientSize.Width * scale);
                h = (int)Math.Floor(w * Player.VideoSize.Height / (double)Player.VideoSize.Width);
            }

            SetSize(w, h, Screen.FromControl(this), false);
        });
    }

    void GuiCommand_MoveWindow(string direction)
    {
        BeginInvoke(() => {
            Screen screen = Screen.FromControl(this);
            Rectangle workingArea = GetWorkingArea(Handle, screen.WorkingArea);

            switch (direction)
            {
                case "left":
                    Left = workingArea.Left;
                    break;
                case "top":
                    Top = 0;
                    break;
                case "right":
                    Left = workingArea.Width - Width + workingArea.Left;
                    break;
                case "bottom":
                    Top = workingArea.Height - Height;
                    break;
                case "center":
                    Left = (screen.Bounds.Width - Width) / 2;
                    Top = (screen.Bounds.Height - Height) / 2;
                    break;
            }
        });
    }

    void GuiCommand_WindowScaleNet(float scale)
    {
        BeginInvoke(() => {
            SetSize(
                (int)(Player.VideoSize.Width * scale),
                (int)Math.Floor(Player.VideoSize.Height * scale),
                Screen.FromControl(this), false);
            Player.Command($"show-text \"window-scale {scale.ToString(CultureInfo.InvariantCulture)}\"");
        });
    }

    void GuiCommand_ShowMenu()
    {
        BeginInvoke(() => {
            if (IsMouseInOsc())
                return;

            ShowCursor();
            UpdateMenu();
            ContextMenu.IsOpen = true;
        });
    }
}
