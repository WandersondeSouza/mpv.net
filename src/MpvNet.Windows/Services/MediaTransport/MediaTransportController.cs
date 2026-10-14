namespace MpvNet.Windows.Services.MediaTransport;

public sealed class MediaTransportController : IDisposable
{
    readonly IMediaTransportService _service;
    readonly Action<MediaTransportCommandEventArgs> _commandHandler;
    readonly object _sync = new();
    MediaTransportSnapshot _snapshot = MediaTransportSnapshot.Disabled;
    MediaTransportSnapshot? _published;
    bool _initialized;
    bool _suspended;
    bool _disposed;

    public MediaTransportController(
        IMediaTransportService service,
        Action<MediaTransportCommandEventArgs> commandHandler)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _commandHandler = commandHandler ?? throw new ArgumentNullException(nameof(commandHandler));
        _service.CommandRequested += Service_CommandRequested;
    }

    public bool IsAvailable => !_disposed && _service.IsAvailable;

    public MediaTransportSnapshot Snapshot
    {
        get
        {
            lock (_sync)
                return _snapshot;
        }
    }

    public bool Initialize(nint windowHandle)
    {
        lock (_sync)
        {
            if (_disposed || _initialized || windowHandle == nint.Zero)
                return IsAvailable;

            _initialized = true;
        }

        try
        {
            _service.Initialize(windowHandle);
            PublishCurrentSnapshot();
        }
        catch (Exception ex)
        {
            Log.Debug($"SMTC controller initialization failed: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
        }

        return IsAvailable;
    }

    public void Publish(MediaTransportSnapshot snapshot)
    {
        MediaTransportSnapshot normalized = (snapshot ?? MediaTransportSnapshot.Disabled).Normalize();

        lock (_sync)
        {
            if (_disposed)
                return;

            _snapshot = normalized;

            if (!_initialized || _suspended)
                return;
        }

        TryPublish(normalized);
    }

    public void RequestCommand(MediaTransportCommandEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        Service_CommandRequested(this, args);
    }

    public void Suspend()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _suspended = true;
            _published = null;
        }

        try
        {
            _service.Suspend();
        }
        catch (Exception ex)
        {
            Log.Debug($"SMTC controller suspension failed: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
        }
    }

    public void Resume()
    {
        MediaTransportSnapshot snapshot;

        lock (_sync)
        {
            if (_disposed)
                return;

            _suspended = false;
            snapshot = _snapshot;
        }

        if (_initialized)
            TryPublish(snapshot);
    }

    void PublishCurrentSnapshot()
    {
        MediaTransportSnapshot snapshot;
        lock (_sync)
            snapshot = _snapshot;

        TryPublish(snapshot);
    }

    void TryPublish(MediaTransportSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_disposed || _suspended || snapshot == _published)
                return;

            try
            {
                _service.Publish(snapshot);
                _published = snapshot;
            }
            catch (Exception ex)
            {
                Log.Debug($"SMTC controller publish failed: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
            }
        }
    }

    void Service_CommandRequested(object? sender, MediaTransportCommandEventArgs args)
    {
        MediaTransportCommandEventArgs? accepted = null;

        lock (_sync)
        {
            if (_disposed || _suspended || !_snapshot.IsEnabled || !_snapshot.IsMediaLoaded)
                return;

            switch (args.Command)
            {
                case MediaTransportCommand.Play when _snapshot.CanPlay:
                case MediaTransportCommand.Pause when _snapshot.CanPause:
                case MediaTransportCommand.Stop when _snapshot.CanStop:
                case MediaTransportCommand.Next when _snapshot.CanNext:
                case MediaTransportCommand.Previous when _snapshot.CanPrevious:
                    accepted = new MediaTransportCommandEventArgs(args.Command);
                    break;

                case MediaTransportCommand.Seek when GetSeekPosition(_snapshot, args) is TimeSpan position:
                    accepted = new MediaTransportCommandEventArgs(args.Command, position);
                    break;
                case MediaTransportCommand.FastForward when _snapshot.CanSeek:
                case MediaTransportCommand.Rewind when _snapshot.CanSeek:
                    accepted = new MediaTransportCommandEventArgs(args.Command);
                    break;
            }
        }

        if (accepted != null)
            _commandHandler(accepted);
    }

    // Matches the default Left/Right shortcuts. Re-evaluate against the real player
    // position on the UI thread instead of seeking relative to a five-second snapshot.
    public static TimeSpan? GetSeekPosition(MediaTransportSnapshot snapshot, MediaTransportCommandEventArgs args)
    {
        snapshot = snapshot.Normalize();
        if (!snapshot.CanSeek)
            return null;

        TimeSpan? requested = args.Command switch
        {
            MediaTransportCommand.Seek when args.Position != TimeSpan.MaxValue && args.Position != TimeSpan.MinValue => args.Position,
            MediaTransportCommand.FastForward => snapshot.Duration - snapshot.Position < TimeSpan.FromSeconds(5)
                ? snapshot.Duration : snapshot.Position + TimeSpan.FromSeconds(5),
            MediaTransportCommand.Rewind => snapshot.Position < TimeSpan.FromSeconds(5)
                ? TimeSpan.Zero : snapshot.Position - TimeSpan.FromSeconds(5),
            _ => null,
        };
        return requested is TimeSpan value
            ? value < TimeSpan.Zero ? TimeSpan.Zero : value > snapshot.Duration ? snapshot.Duration : value
            : null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        _service.CommandRequested -= Service_CommandRequested;
        try
        {
            _service.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug($"SMTC controller disposal failed: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
        }
    }
}
