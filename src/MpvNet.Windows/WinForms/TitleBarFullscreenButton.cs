using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using static MpvNet.Windows.Native.WinApi;

namespace MpvNet.Windows.WinForms;

/// <summary>
/// An owned, nonactivating caption surface. The owner's non-client frame, HWND,
/// title and native caption buttons are never replaced. HTTRANSPARENT sends
/// caption gestures back to the owner on the same UI thread.
/// </summary>
public sealed class TitleBarFullscreenButton : Form
{
    readonly Form _owner;
    readonly Button _button;
    readonly ToolTip _tooltip = new() { ShowAlways = true };
    readonly Action _execute;

    public TitleBarFullscreenButton(Form owner, Action execute)
    {
        _owner = owner;
        _execute = execute;
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        _button = new FullscreenButton { TabStop = false, FlatStyle = FlatStyle.Flat };
        _button.FlatAppearance.BorderSize = 0;
        _button.Click += Button_Click;
        Controls.Add(_button);
        owner.HandleDestroyed += Owner_HandleDestroyed;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return parameters;
        }
    }

    public void UpdateCaption(bool enabled, bool darkMode, string accessibleName)
    {
        if (IsDisposed)
            return;

        if (!enabled || !_owner.IsHandleCreated || !_owner.Visible ||
            _owner.WindowState == FormWindowState.Minimized || !TryGetBounds(out Rectangle bounds, out int buttonWidth))
        {
            HideCaption();
            return;
        }

        BackColor = SystemInformation.HighContrast ? SystemColors.ActiveCaption :
            darkMode ? Color.FromArgb(32, 32, 32) : Color.White;
        ForeColor = SystemInformation.HighContrast ? SystemColors.ActiveCaptionText :
            darkMode ? Color.White : Color.Black;
        _button.BackColor = BackColor;
        _button.ForeColor = ForeColor;
        _button.FlatAppearance.MouseOverBackColor = darkMode ? Color.FromArgb(55, 55, 55) : Color.FromArgb(229, 229, 229);
        _button.FlatAppearance.MouseDownBackColor = darkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(204, 204, 204);
        if (_button.AccessibleName != accessibleName)
        {
            Text = accessibleName;
            _button.AccessibleName = accessibleName;
            _button.AccessibleRole = AccessibleRole.PushButton;
            _tooltip.SetToolTip(_button, accessibleName);
        }

        // Cover only the added button. The native caption remains responsible
        // for drawing the title, avoiding duplicate text over the same area.
        Bounds = new Rectangle(bounds.Right - buttonWidth, bounds.Top, buttonWidth, bounds.Height);
        _button.Bounds = ClientRectangle;
        if (!Visible)
            Show(_owner);
        Invalidate();
    }

    bool TryGetBounds(out Rectangle bounds, out int buttonWidth)
    {
        bounds = Rectangle.Empty;
        buttonWidth = 0;
        TitleBarInfo info = new() { Size = Marshal.SizeOf<TitleBarInfo>(), States = new uint[6], Rectangles = new RECT[6] };
        GetTitleBarInfo(_owner.Handle, 0x033F /* WM_GETTITLEBARINFOEX */, IntPtr.Zero, ref info);
        if ((info.States[0] & 0x18000) != 0 || (info.States[2] & 0x18000) != 0)
            return false;

        // DWM bounds are window-relative; TITLEBARINFOEX and client origin are
        // screen coordinates. Keep signed coordinates for monitors left/above primary.
        if (!GetWindowRect(_owner.Handle, out RECT window) ||
            DwmGetWindowAttribute(_owner.Handle, 5 /* DWMWA_CAPTION_BUTTON_BOUNDS */, out RECT buttons, 16) != 0)
            return false;

        Rectangle nativeButtons = buttons.ToRectangle();
        nativeButtons.Offset(window.Left, window.Top);
        int right = Math.Min(nativeButtons.Left, info.Rectangles[2].Left);
        int top = Math.Max(nativeButtons.Top, info.TitleBar.Top);
        int bottom = Math.Min(nativeButtons.Bottom, _owner.PointToScreen(Point.Empty).Y);
        buttonWidth = info.Rectangles[2].Width;
        // Leave the native small icon and its spacing visible at the current DPI.
        int dpi = GetDpiForWindow(_owner.Handle);
        int iconSpace = GetSystemMetricsForDpi(49 /* SM_CXSMICON */, dpi) + (4 * dpi + 95) / 96;
        bounds = Rectangle.FromLTRB(info.TitleBar.Left + iconSpace, top, right, bottom);
        return buttonWidth > 0 && bounds.Width > buttonWidth && bounds.Height > 0;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0084) // WM_NCHITTEST: only the new button consumes input.
        {
            Point point = PointToClient(new Point(unchecked((short)m.LParam.ToInt64()), unchecked((short)(m.LParam.ToInt64() >> 16))));
            if (!_button.Bounds.Contains(point))
            {
                m.Result = new IntPtr(-1); // HTTRANSPARENT
                return;
            }
        }
        if (m.Msg == 0x0021) // WM_MOUSEACTIVATE
        {
            m.Result = new IntPtr(3); // MA_NOACTIVATE: retain keyboard/Esc focus on the player.
            return;
        }
        base.WndProc(ref m);
    }

    void Button_Click(object? sender, EventArgs e)
    {
        _owner.Activate();
        _execute();
    }
    public void HideCaption()
    {
        _tooltip.Hide(_button);
        Hide();
    }

    void Owner_HandleDestroyed(object? sender, EventArgs e) => HideCaption();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _owner.HandleDestroyed -= Owner_HandleDestroyed;
            _button.Click -= Button_Click;
            _tooltip.Dispose();
        }
        base.Dispose(disposing);
    }

    sealed class FullscreenButton : Button
    {
        public FullscreenButton() => SetStyle(ControlStyles.Selectable, false);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Outline of the bundled OSC's classic E108 glyph, mpv-osd-symbols:
            // mpv-player/mpv TOOLS/mpv-osd-symbols.sfdir/uniE108.glyph (83659e38).
            // Reproduce the path, because libmpv's internal font is not a GDI font.
            using GraphicsPath path = new(FillMode.Alternate);
            path.AddLine(0, 580, 0, 60);
            path.AddBezier(0, 60, 0, 30, 30, 0, 60, 0);
            path.AddLine(60, 0, 820, 0);
            path.AddBezier(820, 0, 850, 0, 880, 30, 880, 60);
            path.AddLine(880, 60, 880, 580);
            path.AddBezier(880, 580, 880, 610, 850, 640, 820, 640);
            path.AddLine(820, 640, 60, 640);
            path.AddBezier(60, 640, 30, 640, 0, 610, 0, 580);
            path.CloseFigure();
            path.AddRectangle(new Rectangle(60, 60, 760, 520));
            path.AddPolygon(new[] { new Point(580, 120), new Point(760, 120), new Point(760, 300) });
            path.AddRectangle(new Rectangle(274, 240, 332, 159));
            path.AddPolygon(new[] { new Point(300, 520), new Point(120, 520), new Point(120, 340) });
            float width = Math.Min(18f * DeviceDpi / 96, Width - 8);
            float height = width * 640 / 880;
            using Matrix transform = new(width / 880, 0, 0, height / 640, (Width - width) / 2, (Height - height) / 2);
            path.Transform(transform);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using SolidBrush brush = new(ForeColor);
            e.Graphics.FillPath(brush, path);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct TitleBarInfo
    {
        public int Size;
        public RECT TitleBar;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public uint[] States;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public RECT[] Rectangles;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    static extern IntPtr GetTitleBarInfo(IntPtr hwnd, int message, IntPtr wparam, ref TitleBarInfo info);

    [DllImport("user32.dll")]
    static extern int GetSystemMetricsForDpi(int index, int dpi);
}
