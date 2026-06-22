
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MpvNet.Windows.Help;
using MpvNet.Windows.Native;

namespace MpvNet.Windows.WinForms;

public class SnapManager
{
    int _dragOffsetX;
    int _dragOffsetY;

    IntPtr _handle;

    [Flags]
    public enum SnapLocation
    {
        None   = 0,
        Left   = 1 << 0,
        Top    = 1 << 1,
        Right  = 1 << 2,
        Bottom = 1 << 3,
        All = Left | Top | Right | Bottom
    }

    public int AnchorDistance { get; set; }

    public int SnapDistance { get; set; }

    bool InSnapRange(int a, int b) => Math.Abs(a - b) < SnapDistance;

    void FindSnap(ref Rectangle effectiveBounds)
    {
        Screen currentScreen = Screen.FromPoint(effectiveBounds.Location);
        Rectangle workingArea = WinApiHelp.GetWorkingArea(_handle, currentScreen.WorkingArea);

        if (InSnapRange(effectiveBounds.Left, workingArea.Left + AnchorDistance))
            effectiveBounds.X = workingArea.Left + AnchorDistance;
        else if (InSnapRange(effectiveBounds.Right, workingArea.Right - AnchorDistance))
            effectiveBounds.X = workingArea.Right - AnchorDistance - effectiveBounds.Width;
        if (InSnapRange(effectiveBounds.Top, workingArea.Top + AnchorDistance))
            effectiveBounds.Y = workingArea.Top + AnchorDistance;
        else if (InSnapRange(effectiveBounds.Bottom, workingArea.Bottom - AnchorDistance))
            effectiveBounds.Y = workingArea.Bottom - AnchorDistance - effectiveBounds.Height;
    }

    public void OnMoving(ref Message m)
    {
        if (_handle == IntPtr.Zero)
            return;

        WinApi.RECT boundsLtrb = Marshal.PtrToStructure<WinApi.RECT>(m.LParam);
        Rectangle bounds = boundsLtrb.ToRectangle();
        // This is where the window _would_ be located if snapping
        // had not occurred. This prevents the cursor from sliding
        // off the title bar if the snap distance is too large.
        Rectangle effectiveBounds = new Rectangle(
            Cursor.Position.X - _dragOffsetX,
            Cursor.Position.Y - _dragOffsetY,
            bounds.Width,
            bounds.Height);
        FindSnap(ref effectiveBounds);
        WinApi.RECT newLtrb = WinApi.RECT.FromRectangle(effectiveBounds);
        Marshal.StructureToPtr(newLtrb, m.LParam, false);
        m.Result = new IntPtr(1);
    }

    public void OnSizeAndEnterSizeMove(Form form)
    {
        _handle = form.Handle;
        SnapDistance = form.Font.Height;
        // Need to handle window size changed as well when
        // un-maximizing the form by dragging the title bar.
        _dragOffsetX = Cursor.Position.X - form.Left;
        _dragOffsetY = Cursor.Position.Y - form.Top;
    }
}
