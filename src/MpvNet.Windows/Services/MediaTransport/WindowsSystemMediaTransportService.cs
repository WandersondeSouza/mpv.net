using Windows.Foundation;
using Windows.Media;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed class WindowsSystemMediaTransportService : IMediaTransportService
{
    readonly object _sync = new();
    SystemMediaTransportControls? _controls;
    TypedEventHandler<SystemMediaTransportControls, SystemMediaTransportControlsButtonPressedEventArgs>? _buttonPressedHandler;
    TypedEventHandler<SystemMediaTransportControls, PlaybackPositionChangeRequestedEventArgs>? _positionChangeRequestedHandler;
    bool _disposed;

    public bool IsAvailable
    {
        get
        {
            lock (_sync)
                return !_disposed && _controls != null;
        }
    }

    public event EventHandler<MediaTransportCommandEventArgs>? CommandRequested;

    public void Initialize(nint windowHandle)
    {
        lock (_sync)
        {
            if (_disposed || _controls != null || windowHandle == nint.Zero)
                return;

            try
            {
                _controls = SystemMediaTransportControlsInterop.GetForWindow(windowHandle);
                _buttonPressedHandler = (_, args) => OnButtonPressed(args.Button);
                _positionChangeRequestedHandler = (_, args) => OnPositionChangeRequested(args.RequestedPlaybackPosition);
                _controls.ButtonPressed += _buttonPressedHandler;
                _controls.PlaybackPositionChangeRequested += _positionChangeRequestedHandler;
            }
            catch (Exception ex)
            {
                _controls = null;
                _buttonPressedHandler = null;
                _positionChangeRequestedHandler = null;
                Log.Debug($"SMTC indisponivel: {ex.GetType().Name}");
            }
        }
    }

    public void Publish(MediaTransportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            if (_disposed || _controls == null)
                return;

            try
            {
                MediaTransportSnapshot normalized = snapshot.Normalize();
                if (!normalized.IsEnabled || !normalized.IsMediaLoaded || normalized.Metadata == null)
                {
                    DisableUnsafe();
                    return;
                }

                PublishMetadataUnsafe(normalized.Metadata);
                _controls.IsEnabled = true;
                _controls.IsPlayEnabled = normalized.CanPlay;
                _controls.IsPauseEnabled = normalized.CanPause;
                _controls.IsStopEnabled = normalized.CanStop;
                _controls.IsPreviousEnabled = normalized.CanPrevious;
                _controls.IsNextEnabled = normalized.CanNext;
                _controls.PlaybackStatus = ToWindowsPlaybackStatus(normalized.PlaybackStatus);
                UpdateTimelineUnsafe(normalized);
            }
            catch (Exception ex)
            {
                Log.Debug($"SMTC nao pode publicar estado: {ex.GetType().Name}");
                try
                {
                    DisableUnsafe();
                }
                catch
                {
                    // The integration remains fail-open if the system session is gone.
                }
            }
        }
    }

    public void Suspend()
    {
        lock (_sync)
        {
            if (_disposed || _controls == null)
                return;

            try
            {
                DisableUnsafe();
            }
            catch (Exception ex)
            {
                Log.Debug($"SMTC nao pode ser suspenso: {ex.GetType().Name}");
            }
        }
    }

    void PublishMetadataUnsafe(MediaTransportMetadata metadata)
    {
        SystemMediaTransportControlsDisplayUpdater updater = _controls!.DisplayUpdater;
        updater.ClearAll();
        updater.AppMediaId = "mpvnet";
        updater.Thumbnail = null;
        updater.Type = metadata.MediaType switch
        {
            MediaTransportMediaType.Video => MediaPlaybackType.Video,
            MediaTransportMediaType.Music => MediaPlaybackType.Music,
            _ => MediaPlaybackType.Unknown,
        };

        if (metadata.MediaType == MediaTransportMediaType.Video)
        {
            VideoDisplayProperties properties = updater.VideoProperties;
            properties.Title = metadata.DisplayTitle;
            properties.Subtitle = metadata.Subtitle ?? "MPV.NET";
        }
        else if (metadata.MediaType == MediaTransportMediaType.Music)
        {
            MusicDisplayProperties properties = updater.MusicProperties;
            properties.Title = metadata.DisplayTitle;
            properties.Artist = metadata.Artist ?? "";
            properties.AlbumTitle = metadata.Album ?? "";
            properties.TrackNumber = metadata.TrackNumber ?? 0;
        }

        updater.Update();
    }

    void UpdateTimelineUnsafe(MediaTransportSnapshot snapshot)
    {
        TimeSpan duration = snapshot.Duration;
        TimeSpan position = snapshot.Position;

        if (duration <= TimeSpan.Zero || duration == TimeSpan.MaxValue)
        {
            _controls!.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties());
            return;
        }

        if (position < TimeSpan.Zero)
            position = TimeSpan.Zero;
        if (position > duration)
            position = duration;

        _controls!.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
        {
            StartTime = TimeSpan.Zero,
            EndTime = duration,
            Position = position,
            MinSeekTime = TimeSpan.Zero,
            MaxSeekTime = duration,
        });
    }

    void DisableUnsafe()
    {
        _controls!.IsPlayEnabled = false;
        _controls.IsPauseEnabled = false;
        _controls.IsStopEnabled = false;
        _controls.IsPreviousEnabled = false;
        _controls.IsNextEnabled = false;
        _controls.PlaybackStatus = MediaPlaybackStatus.Closed;
        _controls.IsEnabled = false;
        _controls.DisplayUpdater.ClearAll();
        _controls.DisplayUpdater.Thumbnail = null;
        _controls.DisplayUpdater.Update();
        _controls.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties());
    }

    void OnButtonPressed(SystemMediaTransportControlsButton button)
    {
        MediaTransportCommand? command = button switch
        {
            SystemMediaTransportControlsButton.Play => MediaTransportCommand.Play,
            SystemMediaTransportControlsButton.Pause => MediaTransportCommand.Pause,
            SystemMediaTransportControlsButton.Stop => MediaTransportCommand.Stop,
            SystemMediaTransportControlsButton.Next => MediaTransportCommand.Next,
            SystemMediaTransportControlsButton.Previous => MediaTransportCommand.Previous,
            _ => null,
        };

        if (command is MediaTransportCommand value)
            CommandRequested?.Invoke(this, new MediaTransportCommandEventArgs(value));
    }

    void OnPositionChangeRequested(TimeSpan position) =>
        CommandRequested?.Invoke(this, new MediaTransportCommandEventArgs(MediaTransportCommand.Seek, position));

    static MediaPlaybackStatus ToWindowsPlaybackStatus(MediaTransportPlaybackStatus status) => status switch
    {
        MediaTransportPlaybackStatus.Playing => MediaPlaybackStatus.Playing,
        MediaTransportPlaybackStatus.Paused => MediaPlaybackStatus.Paused,
        MediaTransportPlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
        _ => MediaPlaybackStatus.Closed,
    };

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_controls != null)
            {
                try
                {
                    if (_buttonPressedHandler != null)
                        _controls.ButtonPressed -= _buttonPressedHandler;
                    if (_positionChangeRequestedHandler != null)
                        _controls.PlaybackPositionChangeRequested -= _positionChangeRequestedHandler;
                    DisableUnsafe();
                }
                catch (Exception ex)
                {
                    Log.Debug($"SMTC nao pode ser liberado completamente: {ex.GetType().Name}");
                }
            }

            _controls = null;
            _buttonPressedHandler = null;
            _positionChangeRequestedHandler = null;
            CommandRequested = null;
        }
    }
}
