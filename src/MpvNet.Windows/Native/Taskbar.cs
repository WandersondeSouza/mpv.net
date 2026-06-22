
using System.Runtime.InteropServices;

public class Taskbar : IDisposable
{
    public IntPtr Handle { get; set; }

    public Taskbar(IntPtr handle) => Handle = handle;
 
    readonly ITaskbarList3 _instance = (ITaskbarList3)new TaskBarCommunication();
    bool _disposed;

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

    public void Dispose()
    {
        if (_disposed)
            return;

        if (Marshal.IsComObject(_instance))
            Marshal.FinalReleaseComObject(_instance);

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
