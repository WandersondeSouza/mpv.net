namespace MpvNet.Windows.Services.MediaTransport;

public sealed class MediaTransportController : IDisposable
{
    readonly IMediaTransportService _service;
    readonly Action<MediaTransportCommandEventArgs> _commandHandler;
    readonly object _sync = new();
    MediaTransportSnapshot _snapshot = MediaTransportSnapshot.Disabled;
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
        catch
        {
            // The player must remain usable when SMTC is unavailable on the host.
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
        }

        try
        {
            _service.Suspend();
        }
        catch
        {
            // Best-effort teardown; SMTC must never stop playback or close the window.
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
        try
        {
            _service.Publish(snapshot);
        }
        catch
        {
            // Publishing is an optional integration and is intentionally fail-open.
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

                case MediaTransportCommand.Seek when args.Position is TimeSpan requested &&
                    _snapshot.Duration > TimeSpan.Zero:
                    TimeSpan position = requested < TimeSpan.Zero ? TimeSpan.Zero : requested;
                    if (position > _snapshot.Duration)
                        position = _snapshot.Duration;

                    accepted = new MediaTransportCommandEventArgs(MediaTransportCommand.Seek, position);
                    break;
            }
        }

        if (accepted != null)
            _commandHandler(accepted);
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
        catch
        {
            // Disposal is best-effort because the integration is optional.
        }
    }
}
