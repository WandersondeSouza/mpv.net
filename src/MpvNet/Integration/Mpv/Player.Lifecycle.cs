using System.Threading;
using System.Threading.Tasks;

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
            LifecycleState = PlayerLifecycleState.Destroyed;
            Log.Debug("Destroying mpv player.");

            _playerCancellation.Cancel();
            Task[] playerTasks;
            lock (_playerTasksLock)
                playerTasks = [.. _playerTasks];
            lock (_eventTasksLock)
                playerTasks = [.. playerTasks, .. _eventTasks];

            try
            {
                Task.WhenAll(playerTasks).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Player background tasks did not finish during shutdown.");
            }

            nint mainHandle = MainHandle;
            nint clientHandle = Handle;
            DestroyHandle(clientHandle);
            DestroyMainHandle(mainHandle);
            MainHandle = IntPtr.Zero;
            Handle = IntPtr.Zero;

            foreach (MpvClient client in Clients)
                client.DestroyHandle();

            Clients.Clear();
        }
    }

    void DestroyMainHandle(nint handle)
    {
        if (handle == IntPtr.Zero)
            return;

        if (_mpvInitialized)
            mpv_terminate_destroy(handle);
        else
            mpv_destroy(handle);
    }

    static void DestroyHandle(nint handle)
    {
        if (handle == IntPtr.Zero)
            return;

        mpv_destroy(handle);
    }

    public void MainEventLoop(CancellationToken cancellationToken)
    {
        nint handle = MainHandle;
        while (handle != IntPtr.Zero && !cancellationToken.IsCancellationRequested)
        {
            mpv_wait_event(handle, 0.1);
        }
    }

    protected override void OnShutdown()
    {
        Log.Debug($"mpv shutdown event received. path='{Log.SafeValue(GetPropertyString("path"))}', playlistPos={GetPropertyInt("playlist-pos")}, playlistCount={GetPropertyInt("playlist-count")}, isQuitNeeded={IsQuitNeeded}");
        IsQuitNeeded = false;
        LifecycleState = PlayerLifecycleState.Shutdown;
        base.OnShutdown();
        ShutdownAutoResetEvent.Set();
    }
}
