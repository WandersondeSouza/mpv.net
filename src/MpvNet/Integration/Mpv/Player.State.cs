using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

public partial class MainPlayer
{
    static readonly TimeSpan PlaylistNormalizationDelay = TimeSpan.FromMilliseconds(200);
    bool _isNormalizingAutocreatedPlaylist;
    readonly object _destroyLock = new();
    bool _isDestroyed;
    readonly CancellationTokenSource _playerCancellation = new();
    readonly SemaphoreSlim _playerTaskGate = new(1, 1);
    readonly object _playerTasksLock = new();
    readonly List<Task> _playerTasks = [];
    readonly object _eventTasksLock = new();
    readonly List<Task> _eventTasks = [];
    readonly object _autoLoadFolderStateLock = new();
    bool _autoLoadFolderRequested;
    bool _autoLoadFolderInProgress;
    bool _mpvInitialized;

    public PlayerLifecycleState LifecycleState { get; private set; } = PlayerLifecycleState.Created;
    internal CancellationToken PlayerCancellationToken => _playerCancellation.Token;

    public void SchedulePlayerTask(Action<CancellationToken> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        SchedulePlayerTask(cancellationToken =>
        {
            operation(cancellationToken);
            return Task.CompletedTask;
        });
    }

    public void SchedulePlayerTask(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_destroyLock)
        {
            if (_isDestroyed)
                return;

            Task task = Task.Run(() => RunPlayerTaskAsync(operation), _playerCancellation.Token);
            lock (_playerTasksLock)
                _playerTasks.Add(task);
            task.ContinueWith(completedTask =>
            {
                lock (_playerTasksLock)
                    _playerTasks.Remove(completedTask);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    async Task RunPlayerTaskAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await _playerTaskGate.WaitAsync(_playerCancellation.Token).ConfigureAwait(false);
            try
            {
                _playerCancellation.Token.ThrowIfCancellationRequested();
                await operation(_playerCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                _playerTaskGate.Release();
            }
        }
        catch (OperationCanceledException) when (_playerCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Terminal.WriteError(ex);
        }
    }

    internal void TrackEventTask(Task task)
    {
        lock (_destroyLock)
        {
            if (_isDestroyed)
                return;

            lock (_eventTasksLock)
                _eventTasks.Add(task);
        }
        task.ContinueWith(completedTask =>
        {
            lock (_eventTasksLock)
                _eventTasks.Remove(completedTask);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal void SetMpvInitialized() => _mpvInitialized = true;

    internal void ArmAutoLoadFolder(bool enabled)
    {
        lock (_autoLoadFolderStateLock)
            _autoLoadFolderRequested = enabled && !_autoLoadFolderInProgress;
    }

    internal bool TryConsumeAutoLoadFolderRequest()
    {
        lock (_autoLoadFolderStateLock)
        {
            if (!_autoLoadFolderRequested || _autoLoadFolderInProgress)
                return false;

            _autoLoadFolderRequested = false;
            _autoLoadFolderInProgress = true;
            return true;
        }
    }

    internal void FinishAutoLoadFolder()
    {
        lock (_autoLoadFolderStateLock)
            _autoLoadFolderInProgress = false;
    }

    internal static bool ShouldAdvanceAfterPlaybackError(int failedPosition, int currentPosition, int playlistCount) =>
        failedPosition >= 0 &&
        currentPosition == failedPosition &&
        failedPosition + 1 < playlistCount;

    public string ConfPath { get => ConfigFolder + "mpv.conf"; }
    public string CacheFolder => TemporaryFileCleanup.DefaultCacheFolder + System.IO.Path.DirectorySeparatorChar;
    public string GPUAPI { get; set; } = "auto";
    public string Path { get; set; } = "";
    public string VO { get; set; } = "gpu";
    public string UsedInputConfContent { get; set; } = "";

    public string VID { get; set; } = "";
    public string AID { get; set; } = "";
    public string SID { get; set; } = "";

    public bool Border { get; set; } = true;
    public bool FileEnded { get; set; }
    public bool Fullscreen { get; set; }
    public bool IsQuitNeeded { set; get; } = true;
    public bool KeepaspectWindow { get; set; }
    public bool Paused { get; set; }
    public bool SnapWindow { get; set; }
    public bool TaskbarProgress { get; set; } = true;
    public bool TitleBar { get; set; } = true;
    public bool WasInitialSizeSet;
    public bool WindowMaximized { get; set; }
    public bool WindowMinimized { get; set; }

    public int Edition { get; set; }
    public int PlaylistPos { get; set; } = -1;
    public int Screen { get; set; } = -1;
    public int VideoRotate { get; set; }

    public float Autofit { get; set; } = 0.6f;
    public float AutofitSmaller { get; set; } = 0.3f;
    public float AutofitLarger { get; set; } = 0.8f;

    public AutoResetEvent ShutdownAutoResetEvent { get; } = new AutoResetEvent(false);
    public nint MainHandle { get; set; }
    public List<MediaTrack> MediaTracks { get; set; } = new List<MediaTrack>();
    public List<TimeSpan> BluRayTitles { get; } = new List<TimeSpan>();
    public object MediaTracksLock { get; } = new object();
    public Size VideoSize { get; set; }
    public TimeSpan Duration;
    public List<MpvClient> Clients { get; } = new List<MpvClient>();

    List<StringPair>? _audioDevices;

    public event Action? Initialized;
    public event Action? Pause;
    public event Action<int>? PlaylistPosChanged;
    public event Action<Size>? VideoSizeChanged;
}
