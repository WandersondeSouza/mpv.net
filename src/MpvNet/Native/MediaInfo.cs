
using System.Runtime.InteropServices;

namespace MpvNet;

public class MediaInfo : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public MediaInfo(string file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if ((_handle = MediaInfo_New()) == IntPtr.Zero)
            throw new Exception("Failed to call MediaInfo_New");

        if (MediaInfo_Open(_handle, file) == 0)
            throw new Exception("Error MediaInfo_Open");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MediaInfo));
    }

    public string GetInfo(MediaInfoStreamKind kind, string parameter)
    {
        ThrowIfDisposed();

        return Marshal.PtrToStringUni(MediaInfo_Get(_handle, kind, 0,
            parameter, MediaInfoKind.Text, MediaInfoKind.Name)) ?? "";
    }

    public int GetCount(MediaInfoStreamKind kind)
    {
        ThrowIfDisposed();
        return MediaInfo_Count_Get(_handle, kind, -1);
    }

    public string GetGeneral(string parameter)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(parameter);
        return Marshal.PtrToStringUni(MediaInfo_Get(_handle, MediaInfoStreamKind.General,
            0, parameter, MediaInfoKind.Text, MediaInfoKind.Name)) ?? "";
    }

    public string GetVideo(int stream, string parameter)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(parameter);
        return Marshal.PtrToStringUni(MediaInfo_Get(_handle, MediaInfoStreamKind.Video,
            stream, parameter, MediaInfoKind.Text, MediaInfoKind.Name)) ?? "";
    }

    public string GetAudio(int stream, string parameter)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(parameter);
        return Marshal.PtrToStringUni(MediaInfo_Get(_handle, MediaInfoStreamKind.Audio,
            stream, parameter, MediaInfoKind.Text, MediaInfoKind.Name)) ?? "";
    }

    public string GetText(int stream, string parameter)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(parameter);
        return Marshal.PtrToStringUni(MediaInfo_Get(_handle, MediaInfoStreamKind.Text,
            stream, parameter, MediaInfoKind.Text, MediaInfoKind.Name)) ?? "";
    }

    public string GetSummary(bool complete, bool rawView)
    {
        ThrowIfDisposed();
        MediaInfo_Option(_handle, "Language", rawView ? "raw" : "");
        MediaInfo_Option(_handle, "Complete", complete ? "1" : "0");
        return Marshal.PtrToStringUni(MediaInfo_Inform(_handle, 0)) ?? "";
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_handle != IntPtr.Zero)
            {
                MediaInfo_Close(_handle);
                MediaInfo_Delete(_handle);
                _handle = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    ~MediaInfo()
    {
        Dispose(disposing: false);
    }

    [DllImport("MediaInfo.dll")]
    static extern IntPtr MediaInfo_New();

    [DllImport("MediaInfo.dll", CharSet = CharSet.Unicode)]
    static extern int MediaInfo_Open(IntPtr handle, string path);

    [DllImport("MediaInfo.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr MediaInfo_Option(IntPtr handle, string option, string value);

    [DllImport("MediaInfo.dll")]
    static extern IntPtr MediaInfo_Inform(IntPtr handle, int reserved);

    [DllImport("MediaInfo.dll")]
    static extern int MediaInfo_Close(IntPtr handle);

    [DllImport("MediaInfo.dll")]
    static extern void MediaInfo_Delete(IntPtr handle);

    [DllImport("MediaInfo.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr MediaInfo_Get(IntPtr handle, MediaInfoStreamKind kind,
        int stream, string parameter, MediaInfoKind infoKind, MediaInfoKind searchKind);

    [DllImport("MediaInfo.dll", CharSet = CharSet.Unicode)]
    static extern int MediaInfo_Count_Get(IntPtr handle, MediaInfoStreamKind streamKind, int stream);
}

public enum MediaInfoStreamKind
{
    General,
    Video,
    Audio,
    Text,
    Other,
    Image,
    Menu,
    Max,
}

public enum MediaInfoKind
{
    Name,
    Text,
    Measure,
    Options,
    NameText,
    MeasureText,
    Info,
    HowTo
}
