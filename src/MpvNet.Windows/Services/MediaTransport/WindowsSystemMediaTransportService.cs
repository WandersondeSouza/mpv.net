using Windows.Foundation;
using Windows.Media;
using Windows.Storage.Streams;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed class WindowsSystemMediaTransportService : IMediaTransportService
{
    readonly object _sync = new();
    SystemMediaTransportControls? _controls;
    TypedEventHandler<SystemMediaTransportControls, SystemMediaTransportControlsButtonPressedEventArgs>? _buttonPressedHandler;
    TypedEventHandler<SystemMediaTransportControls, PlaybackPositionChangeRequestedEventArgs>? _positionChangeRequestedHandler;
    bool _disposed;
    bool _positionChangeSubscribed;
    MediaTransportSnapshot? _published;
    CancellationTokenSource? _thumbnailCancellation;
    IRandomAccessStream? _thumbnailStream;
    readonly Func<MediaTransportMetadata, CancellationToken, Task<IRandomAccessStream?>> _thumbnailLoader;

    public WindowsSystemMediaTransportService(
        Func<MediaTransportMetadata, CancellationToken, Task<IRandomAccessStream?>>? thumbnailLoader = null)
    {
        _thumbnailLoader = thumbnailLoader ?? MediaTransportThumbnailLoader.LoadAsync;
    }

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
                Log.Debug($"SMTC initialized. windowHandle={windowHandle}");
            }
            catch (Exception ex)
            {
                _controls = null;
                _buttonPressedHandler = null;
                _positionChangeRequestedHandler = null;
                Log.Debug($"SMTC indisponivel: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
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

                if (_published?.Metadata != normalized.Metadata)
                {
                    Log.Debug($"SMTC metadata changed. type={normalized.Metadata.MediaType}, localCover={normalized.Metadata.ThumbnailPath != null}");
                    ClearThumbnailUnsafe();
                    PublishMetadataUnsafe(normalized.Metadata);
                    StartThumbnailUnsafe(normalized.Metadata);
                }
                if (_published?.IsEnabled != true) _controls.IsEnabled = true;
                if (_published?.CanPlay != normalized.CanPlay) _controls.IsPlayEnabled = normalized.CanPlay;
                if (_published?.CanPause != normalized.CanPause) _controls.IsPauseEnabled = normalized.CanPause;
                if (_published?.CanStop != normalized.CanStop) _controls.IsStopEnabled = normalized.CanStop;
                if (_published?.CanPrevious != normalized.CanPrevious) _controls.IsPreviousEnabled = normalized.CanPrevious;
                if (_published?.CanNext != normalized.CanNext) _controls.IsNextEnabled = normalized.CanNext;
                if (_published?.CanSeek != normalized.CanSeek)
                    SetSeekEnabledUnsafe(normalized.CanSeek);
                if (_published?.PlaybackStatus != normalized.PlaybackStatus)
                    _controls.PlaybackStatus = ToWindowsPlaybackStatus(normalized.PlaybackStatus);
                UpdateTimelineUnsafe(normalized);
                _published = normalized;
            }
            catch (Exception ex)
            {
                Log.Debug($"SMTC nao pode publicar estado: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
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
                Log.Debug($"SMTC nao pode ser suspenso: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
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
            properties.AlbumArtist = metadata.AlbumArtist ?? "";
            properties.TrackNumber = metadata.TrackNumber ?? 0;
        }

        updater.Update();
    }

    void UpdateTimelineUnsafe(MediaTransportSnapshot snapshot)
    {
        TimeSpan duration = snapshot.Duration;
        TimeSpan position = snapshot.Position;

        if (!snapshot.CanSeek)
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
        ClearThumbnailUnsafe();
        if (_published == MediaTransportSnapshot.Disabled)
            return;

        _controls!.IsPlayEnabled = false;
        _controls.IsPauseEnabled = false;
        _controls.IsStopEnabled = false;
        _controls.IsPreviousEnabled = false;
        _controls.IsNextEnabled = false;
        SetSeekEnabledUnsafe(false);
        _controls.PlaybackStatus = MediaPlaybackStatus.Closed;
        _controls.IsEnabled = false;
        _controls.DisplayUpdater.ClearAll();
        _controls.DisplayUpdater.Thumbnail = null;
        _controls.DisplayUpdater.Update();
        _controls.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties());
        _published = MediaTransportSnapshot.Disabled;
    }

    void SetSeekEnabledUnsafe(bool enabled)
    {
        _controls!.IsFastForwardEnabled = enabled;
        _controls.IsRewindEnabled = enabled;
        if (enabled == _positionChangeSubscribed || _positionChangeRequestedHandler == null)
            return;

        if (enabled)
            _controls.PlaybackPositionChangeRequested += _positionChangeRequestedHandler;
        else
            _controls.PlaybackPositionChangeRequested -= _positionChangeRequestedHandler;
        _positionChangeSubscribed = enabled;
    }

    void ClearThumbnailUnsafe()
    {
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = null;
        IRandomAccessStream? stream = _thumbnailStream;
        _thumbnailStream = null;
        try
        {
            if (_controls != null)
                _controls.DisplayUpdater.Thumbnail = null;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    void StartThumbnailUnsafe(MediaTransportMetadata metadata)
    {
        _thumbnailCancellation = new CancellationTokenSource();
        CancellationToken token = _thumbnailCancellation.Token;
        // All file access and image inspection run off the player/UI thread.
        Task.Run(async () =>
        {
            IRandomAccessStream? stream = null;
            try
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    stream = await _thumbnailLoader(metadata, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    Log.Debug($"SMTC cover unavailable: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
                }
                bool defaultIcon = stream == null;
                stream ??= await MediaTransportThumbnailLoader.LoadDefaultIconAsync(token).ConfigureAwait(false);
                lock (_sync)
                {
                    if (stream == null || token.IsCancellationRequested || _disposed || _controls == null ||
                        _published?.Metadata != metadata)
                        return;

                    _controls.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromStream(stream);
                    _controls.DisplayUpdater.Update();
                    Log.Debug($"SMTC thumbnail published. applicationIcon={defaultIcon}");
                    _thumbnailStream = stream;
                    stream = null; // Owned until media changes or the service closes.
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                // A missing/bad cover must never disable transport controls.
                Log.Debug($"SMTC thumbnail unavailable: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
            }
            finally
            {
                stream?.Dispose();
            }
        });
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
            SystemMediaTransportControlsButton.FastForward => MediaTransportCommand.FastForward,
            SystemMediaTransportControlsButton.Rewind => MediaTransportCommand.Rewind,
            _ => null,
        };

        if (command is MediaTransportCommand value)
            RaiseCommand(new MediaTransportCommandEventArgs(value));
    }

    void OnPositionChangeRequested(TimeSpan position) =>
        RaiseCommand(new MediaTransportCommandEventArgs(MediaTransportCommand.Seek, position));

    void RaiseCommand(MediaTransportCommandEventArgs args)
    {
        EventHandler<MediaTransportCommandEventArgs>? handler;
        lock (_sync)
            handler = _disposed ? null : CommandRequested;
        handler?.Invoke(this, args);
    }

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
                    if (_positionChangeSubscribed && _positionChangeRequestedHandler != null)
                    {
                        _controls.PlaybackPositionChangeRequested -= _positionChangeRequestedHandler;
                        _positionChangeSubscribed = false;
                    }
                    DisableUnsafe();
                }
                catch (Exception ex)
                {
                    Log.Debug($"SMTC nao pode ser liberado completamente: {ex.GetType().Name}, hresult=0x{ex.HResult:X8}");
                }
            }

            _controls = null;
            _buttonPressedHandler = null;
            _positionChangeRequestedHandler = null;
            CommandRequested = null;
        }
    }
}
