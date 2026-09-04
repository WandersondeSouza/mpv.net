using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using MpvNet;

public enum TaskbarThumbnailButtonId
{
    Previous = 1,
    PlayPause = 2,
    Next = 3,
    Stop = 4,
    Donation = 5,
}

public enum TaskbarThumbnailIcon
{
    Previous,
    Play,
    Pause,
    Next,
    Stop,
    Donation,
}

public readonly record struct TaskbarThumbnailButton(
    TaskbarThumbnailButtonId Id,
    TaskbarThumbnailIcon Icon,
    string Tooltip,
    bool IsEnabled);

public class Taskbar : IDisposable
{
    const uint ThumbButtonMaskIcon = 0x0002;
    const uint ThumbButtonMaskTooltip = 0x0004;
    const uint ThumbButtonMaskFlags = 0x0008;
    const uint ThumbButtonFlagEnabled = 0x0000;
    const uint ThumbButtonFlagDisabled = 0x0001;
    const uint ThumbButtonFlagDismissOnClick = 0x0002;

    public IntPtr Handle { get; set; }

    public Taskbar(IntPtr handle) => Handle = handle;

    readonly ITaskbarList3 _instance = (ITaskbarList3)new TaskBarCommunication();
    readonly Dictionary<TaskbarThumbnailIcon, Icon> _thumbnailIcons = new();
    bool _disposed;
    bool _thumbnailToolbarAdded;

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    interface ITaskbarList3
    {
        // ITaskbarList
        [PreserveSig] void HrInit();
        [PreserveSig] void AddTab(IntPtr hwnd);
        [PreserveSig] void DeleteTab(IntPtr hwnd);
        [PreserveSig] void ActivateTab(IntPtr hwnd);
        [PreserveSig] void SetActiveAlt(IntPtr hwnd);
        // ITaskbarList2
        [PreserveSig] void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        // ITaskbarList3
        [PreserveSig] void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
        [PreserveSig] void SetProgressState(IntPtr hwnd, TaskbarStates state);
        [PreserveSig] int RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        [PreserveSig] int UnregisterTab(IntPtr hwndTab);
        [PreserveSig] int SetTabActive(IntPtr hwndTab, IntPtr hwndMDI);
        [PreserveSig] int ThumbBarAddButtons(
            IntPtr hwnd,
            uint cButtons,
            [In] ThumbButton[] pButton);
        [PreserveSig] int ThumbBarUpdateButtons(
            IntPtr hwnd,
            uint cButtons,
            [In] ThumbButton[] pButton);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ThumbButton
    {
        public uint dwMask;
        public uint iId;
        public uint iBitmap;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szTip;

        public uint dwFlags;
    }

    [ComImport]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    class TaskBarCommunication
    {
    }

    public void SetState(TaskbarStates taskbarState)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _instance.SetProgressState(Handle, taskbarState);
    }

    public void SetValue(double progressValue, double progressMax)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _instance.SetProgressValue(Handle, (ulong)progressValue, (ulong)progressMax);
    }

    public bool TryAddThumbnailButtons(IReadOnlyList<TaskbarThumbnailButton> buttons)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_thumbnailToolbarAdded)
            return true;

        if (buttons.Count is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(buttons), "The taskbar thumbnail toolbar supports one to seven buttons.");

        try
        {
            _instance.HrInit();
            ThrowIfFailed(_instance.ThumbBarAddButtons(Handle, (uint)buttons.Count, CreateNativeButtons(buttons)));
            _thumbnailToolbarAdded = true;
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"Taskbar thumbnail toolbar unavailable: {ex.GetType().Name}");
            return false;
        }
    }

    public void UpdateThumbnailButtons(IReadOnlyList<TaskbarThumbnailButton> buttons)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_thumbnailToolbarAdded)
            return;

        if (buttons.Count is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(buttons), "The taskbar thumbnail toolbar supports one to seven buttons.");

        try
        {
            ThrowIfFailed(_instance.ThumbBarUpdateButtons(Handle, (uint)buttons.Count, CreateNativeButtons(buttons)));
        }
        catch (Exception ex)
        {
            _thumbnailToolbarAdded = false;
            Log.Debug($"Taskbar thumbnail toolbar update failed: {ex.GetType().Name}");
        }
    }

    ThumbButton[] CreateNativeButtons(IReadOnlyList<TaskbarThumbnailButton> buttons)
    {
        ThumbButton[] nativeButtons = new ThumbButton[buttons.Count];

        for (int i = 0; i < buttons.Count; i++)
        {
            TaskbarThumbnailButton button = buttons[i];
            nativeButtons[i] = new ThumbButton
            {
                dwMask = ThumbButtonMaskIcon | ThumbButtonMaskTooltip | ThumbButtonMaskFlags,
                iId = (uint)button.Id,
                iBitmap = 0,
                hIcon = GetThumbnailIcon(button.Icon).Handle,
                szTip = button.Tooltip,
                dwFlags = (button.IsEnabled ? ThumbButtonFlagEnabled : ThumbButtonFlagDisabled) |
                    ThumbButtonFlagDismissOnClick,
            };
        }

        return nativeButtons;
    }

    Icon GetThumbnailIcon(TaskbarThumbnailIcon icon)
    {
        if (_thumbnailIcons.TryGetValue(icon, out Icon? existing))
            return existing;

        Icon created = CreateThumbnailIcon(icon);
        _thumbnailIcons.Add(icon, created);
        return created;
    }

    static Icon CreateThumbnailIcon(TaskbarThumbnailIcon icon)
    {
        int size = Math.Max(16, SystemInformation.IconSize.Width);
        using Bitmap bitmap = new(size, size, PixelFormat.Format32bppArgb);

        using (Graphics graphics = Graphics.FromImage(bitmap))
        using (Brush brush = new SolidBrush(Color.White))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            float scale = size / 32f;
            graphics.ScaleTransform(scale, scale);

            switch (icon)
            {
                case TaskbarThumbnailIcon.Previous:
                    graphics.FillRectangle(brush, 4, 7, 3, 18);
                    graphics.FillPolygon(brush, new[]
                    {
                        new PointF(16, 7),
                        new PointF(6, 16),
                        new PointF(16, 25),
                    });
                    graphics.FillPolygon(brush, new[]
                    {
                        new PointF(27, 7),
                        new PointF(17, 16),
                        new PointF(27, 25),
                    });
                    break;

                case TaskbarThumbnailIcon.Play:
                    graphics.FillPolygon(brush, new[]
                    {
                        new PointF(9, 6),
                        new PointF(25, 16),
                        new PointF(9, 26),
                    });
                    break;

                case TaskbarThumbnailIcon.Pause:
                    graphics.FillRectangle(brush, 8, 6, 6, 20);
                    graphics.FillRectangle(brush, 18, 6, 6, 20);
                    break;

                case TaskbarThumbnailIcon.Next:
                    graphics.FillPolygon(brush, new[]
                    {
                        new PointF(5, 7),
                        new PointF(15, 16),
                        new PointF(5, 25),
                    });
                    graphics.FillPolygon(brush, new[]
                    {
                        new PointF(16, 7),
                        new PointF(26, 16),
                        new PointF(16, 25),
                    });
                    graphics.FillRectangle(brush, 26, 7, 3, 18);
                    break;

                case TaskbarThumbnailIcon.Stop:
                    graphics.FillRectangle(brush, 8, 8, 16, 16);
                    break;

                case TaskbarThumbnailIcon.Donation:
                    using (GraphicsPath heart = new())
                    {
                        heart.StartFigure();
                        heart.AddBezier(new PointF(16, 27), new PointF(14, 25), new PointF(5, 20), new PointF(5, 13));
                        heart.AddBezier(new PointF(5, 13), new PointF(5, 8), new PointF(11, 6), new PointF(16, 11));
                        heart.AddBezier(new PointF(16, 11), new PointF(21, 6), new PointF(27, 8), new PointF(27, 13));
                        heart.AddBezier(new PointF(27, 13), new PointF(27, 20), new PointF(18, 25), new PointF(16, 27));
                        heart.CloseFigure();
                        graphics.FillPath(brush, heart);
                    }
                    break;
            }
        }

        IntPtr iconHandle = bitmap.GetHicon();
        try
        {
            using Icon source = Icon.FromHandle(iconHandle);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0)
            Marshal.ThrowExceptionForHR(hresult);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        if (_disposed)
            return;

        if (Marshal.IsComObject(_instance))
            Marshal.FinalReleaseComObject(_instance);

        foreach (Icon icon in _thumbnailIcons.Values)
            icon.Dispose();

        _thumbnailIcons.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public enum TaskbarStates
{
    NoProgress = 0,
    Indeterminate = 0x1,
    Normal = 0x2,
    Error = 0x4,
    Paused = 0x8
}
