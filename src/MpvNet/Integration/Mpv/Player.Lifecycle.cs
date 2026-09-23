using System.Runtime.InteropServices;
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
        }

        Log.Debug("Destroying mpv player.");

        BeginShutdown();
        foreach (MpvClient client in Clients)
            client.BeginShutdown();

        _playerCancellation.Cancel();
        Task[] playerTasks;
        lock (_playerTasksLock)
            playerTasks = [.. _playerTasks];
        lock (_eventTasksLock)
            playerTasks = [.. playerTasks, .. _eventTasks];

        try
        {
            Task.WhenAll(playerTasks).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (_playerCancellation.IsCancellationRequested)
        {
            // Cancellation is the expected shutdown path for event loops and delayed work.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Player background tasks did not finish during shutdown.");
        }

        try
        {
            foreach (MpvClient client in Clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "A secondary libmpv client could not be destroyed cleanly.");
                }
            }

            Clients.Clear();
            try
            {
                base.DestroyHandle();
            }
            finally
            {
                nint mainHandle = MainHandle;
                try
                {
                    DestroyMainHandle(mainHandle);
                }
                finally
                {
                    MainHandle = IntPtr.Zero;
                }
            }
        }
        finally
        {
            lock (_playerTasksLock)
                _playerTasks.Clear();
            lock (_eventTasksLock)
                _eventTasks.Clear();

            Initialized = null;
            Pause = null;
            PlaylistPosChanged = null;
            VideoSizeChanged = null;

            CancellationTokenSource? playlistDebounce;
            lock (_playlistNormalizationStateLock)
            {
                playlistDebounce = _playlistNormalizationDebounce;
                _playlistNormalizationDebounce = null;
            }
            playlistDebounce?.Dispose();

            _playerTaskGate.Dispose();
            _playerCancellation.Dispose();
            ShutdownAutoResetEvent.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Destroy();

        base.Dispose(disposing);
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

    public void MainEventLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && TryEnterNativeOperation(out IDisposable? operation))
        {
            bool queueOverflow = false;
            using (operation)
            {
                nint handle = MainHandle;
                if (handle == IntPtr.Zero)
                    return;

                nint eventPointer = mpv_wait_event(handle, 0.1);
                if (eventPointer != IntPtr.Zero)
                {
                    mpv_event_id eventId = (mpv_event_id)Marshal.ReadInt32(eventPointer);
                    queueOverflow = eventId == mpv_event_id.MPV_EVENT_QUEUE_OVERFLOW;
                }
            }

            if (queueOverflow)
                OnQueueOverflow();
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
