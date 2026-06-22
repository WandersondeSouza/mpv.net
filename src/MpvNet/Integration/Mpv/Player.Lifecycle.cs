using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    public void Destroy()
    {
        lock (_destroyLock)
        {
            if (_isDestroyed)
                return;

            _isDestroyed = true;
            Log.Debug("Destroying mpv player.");

            nint mainHandle = MainHandle;
            nint clientHandle = Handle;
            MainHandle = IntPtr.Zero;
            Handle = IntPtr.Zero;
            DestroyHandle(mainHandle);
            DestroyHandle(clientHandle);

            foreach (MpvClient client in Clients)
                client.DestroyHandle();

            Clients.Clear();
        }
    }

    static void DestroyHandle(nint handle)
    {
        if (handle == IntPtr.Zero)
            return;

        mpv_destroy(handle);
    }

    public void MainEventLoop()
    {
        while (true)
        {
            mpv_wait_event(MainHandle, -1);
        }
    }

    protected override void OnShutdown()
    {
        Log.Debug($"mpv shutdown event received. path='{Log.SafeValue(GetPropertyString("path"))}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}, isQuitNeeded={IsQuitNeeded}");
        IsQuitNeeded = false;
        base.OnShutdown();
        ShutdownAutoResetEvent.Set();
    }
}
