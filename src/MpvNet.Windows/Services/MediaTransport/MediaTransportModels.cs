using System.Globalization;

namespace MpvNet.Windows.Services.MediaTransport;

public enum MediaTransportCommand
{
    Play,
    Pause,
    Stop,
    Next,
    Previous,
    Seek,
}

public enum MediaTransportPlaybackStatus
{
    Closed,
    Stopped,
    Playing,
    Paused,
}

public enum MediaTransportMediaType
{
    Unknown,
    Music,
    Video,
}

public sealed record MediaTransportMetadata(
    string Title,
    MediaTransportMediaType MediaType,
    string? Artist = null,
    string? Album = null,
    uint? TrackNumber = null,
    string? Subtitle = null,
    string? ThumbnailPath = null)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Untitled" : Title.Trim();
}

public sealed record MediaTransportSnapshot(
    bool IsEnabled,
    bool IsMediaLoaded,
    MediaTransportPlaybackStatus PlaybackStatus,
    bool CanPlay,
    bool CanPause,
    bool CanStop,
    bool CanPrevious,
    bool CanNext,
    MediaTransportMetadata? Metadata,
    TimeSpan Duration,
    TimeSpan Position)
{
    public static MediaTransportSnapshot Disabled => new(
        IsEnabled: false,
        IsMediaLoaded: false,
        PlaybackStatus: MediaTransportPlaybackStatus.Closed,
        CanPlay: false,
        CanPause: false,
        CanStop: false,
        CanPrevious: false,
        CanNext: false,
        Metadata: null,
        Duration: TimeSpan.Zero,
        Position: TimeSpan.Zero);

    public MediaTransportSnapshot Normalize()
    {
        bool mediaLoaded = IsMediaLoaded && Metadata != null;
        bool enabled = IsEnabled && mediaLoaded;
        TimeSpan duration = IsFiniteNonNegative(Duration) ? Duration : TimeSpan.Zero;
        TimeSpan position = IsFiniteNonNegative(Position) ? Position : TimeSpan.Zero;

        if (duration > TimeSpan.Zero && position > duration)
            position = duration;

        return this with
        {
            IsEnabled = enabled,
            IsMediaLoaded = mediaLoaded,
            CanPlay = enabled && CanPlay,
            CanPause = enabled && CanPause,
            CanStop = enabled && CanStop,
            CanPrevious = enabled && CanPrevious,
            CanNext = enabled && CanNext,
            PlaybackStatus = enabled ? PlaybackStatus : MediaTransportPlaybackStatus.Closed,
            Duration = duration,
            Position = position,
        };
    }

    static bool IsFiniteNonNegative(TimeSpan value) =>
        value >= TimeSpan.Zero && value != TimeSpan.MaxValue;
}

public sealed class MediaTransportCommandEventArgs(MediaTransportCommand command, TimeSpan? position = null) : EventArgs
{
    public MediaTransportCommand Command { get; } = command;
    public TimeSpan? Position { get; } = position;

    public override string ToString() => Position is null
        ? Command.ToString()
        : string.Format(CultureInfo.InvariantCulture, "{0} ({1})", Command, Position.Value);
}

public interface IMediaTransportService : IDisposable
{
    bool IsAvailable { get; }
    event EventHandler<MediaTransportCommandEventArgs>? CommandRequested;
    void Initialize(nint windowHandle);
    void Publish(MediaTransportSnapshot snapshot);
    void Suspend();
}
