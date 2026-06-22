using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    public void Destroy()
    {
        Log.Debug("Destroying mpv player.");
        mpv_destroy(MainHandle);
        mpv_destroy(Handle);

        foreach (var client in Clients)
        {
            mpv_destroy(client.Handle);
        }
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
