using System.Drawing;

using static MpvNet.Windows.Help.WinApiHelp;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    bool IsMouseInOsc()
    {
        Point pos = PointToClient(MousePosition);
        float top = 0;

        if (!Player.Border)
            top = ClientSize.Height * 0.1f;

        return pos.X < ClientSize.Width * 0.1 ||
               pos.X > ClientSize.Width * 0.9 ||
               pos.Y < top ||
               pos.Y > ClientSize.Height * 0.78;
    }

    void ShowCursor()
    {
        if (!_isCursorVisible && _cursorAutohide != -1)
        {
            System.Windows.Forms.Cursor.Show();
            _isCursorVisible = true;
        }
    }

    void HideCursor()
    {
        if (_isCursorVisible && _cursorAutohide != 0)
        {
            System.Windows.Forms.Cursor.Hide();
            _isCursorVisible = false;
        }
    }

    bool IsCursorPosDifferent(Point screenPos)
    {
        float len = 5 * (GetDpi(Handle) / 96f);
        return Math.Abs(screenPos.X - MousePosition.X) > len || Math.Abs(screenPos.Y - MousePosition.Y) > len;
    }
}
