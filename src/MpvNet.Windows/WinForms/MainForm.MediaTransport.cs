using System.Globalization;

using MpvNet;
using MpvNet.Native;
using MpvNet.Windows.Services.MediaTransport;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    void Player_StartFile() => RunOnUiThread(() =>
    {
        _mediaTransportMediaLoaded = false;
        UpdateMediaTransport();
    });

    void Player_EndFile(LibMpv.mpv_end_file_reason reason) => RunOnUiThread(() =>
    {
        _mediaTransportMediaLoaded = false;
        UpdateMediaTransport();
    });

    void Player_Seek() => UpdateMediaTransport();

    void Player_PlaybackRestart() => UpdateMediaTransport();

    void MediaTransportTimer_Tick(object? sender, EventArgs e) => UpdateMediaTransport();

    void HandleMediaTransportCommand(MediaTransportCommandEventArgs args)
    {
        if (InvokeRequired)
        {
            RunOnUiThread(() => HandleMediaTransportCommand(args));
            return;
        }

        if (_managedResourcesDisposed || !_mediaTransportMediaLoaded)
            return;

        switch (args.Command)
        {
            case MediaTransportCommand.Play:
                Player.CommandV("set", "pause", "no");
                break;
            case MediaTransportCommand.Pause:
                Player.CommandV("set", "pause", "yes");
                break;
            case MediaTransportCommand.Stop:
                Player.CommandV("stop");
                break;
            case MediaTransportCommand.Next:
                Player.CommandV("playlist-next", "force");
                break;
            case MediaTransportCommand.Previous:
                Player.CommandV("playlist-prev", "force");
                break;
            case MediaTransportCommand.Seek when args.Position is TimeSpan position:
                Player.CommandV(
                    "seek",
                    position.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                    "absolute");
                break;
        }
    }

    void UpdateMediaTransport()
    {
        if (_mediaTransport == null || _managedResourcesDisposed)
            return;

        if (InvokeRequired)
        {
            RunOnUiThread(UpdateMediaTransport);
            return;
        }

        MediaTransportSnapshot snapshot = BuildMediaTransportSnapshot();
        _mediaTransport.Publish(snapshot);
        UpdateTaskbarThumbnail(snapshot);

        if (snapshot.IsEnabled && snapshot.PlaybackStatus == MediaTransportPlaybackStatus.Playing)
            _mediaTransportTimer?.Start();
        else
            _mediaTransportTimer?.Stop();
    }

    MediaTransportSnapshot BuildMediaTransportSnapshot()
    {
        string path = Player.GetPropertyString("path");
        bool mediaLoaded = _mediaTransportMediaLoaded && !string.IsNullOrWhiteSpace(path);

        if (!mediaLoaded || IsFullscreen)
            return MediaTransportSnapshot.Disabled;

        int playlistCount = Math.Max(0, Player.GetPropertyInt("playlist-count"));
        int playlistPosition = Player.PlaylistPos;
        if (playlistPosition < 0)
            playlistPosition = Player.GetPropertyInt("playlist-pos");

        bool canPrevious = playlistCount > 1 && playlistPosition > 0;
        bool canNext = playlistCount > 1 && playlistPosition >= 0 && playlistPosition + 1 < playlistCount;
        bool paused = Player.Paused;
        MediaTransportPlaybackStatus status = Player.FileEnded
            ? MediaTransportPlaybackStatus.Stopped
            : paused
                ? MediaTransportPlaybackStatus.Paused
                : MediaTransportPlaybackStatus.Playing;

        bool hasVideo = FileTypes.IsVideoFile(path) || !string.IsNullOrWhiteSpace(Player.GetPropertyString("video-codec"));
        bool hasAudio = FileTypes.IsAudioFile(path) || !string.IsNullOrWhiteSpace(Player.GetPropertyString("audio-codec"));
        string? trackText = Player.GetPropertyString("metadata/by-key/track");
        uint? trackNumber = uint.TryParse(trackText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint track)
            && track > 0
                ? track
                : null;

        MediaTransportMetadata metadata = MediaTransportMetadataBuilder.Build(new(
            Path: path,
            MediaTitle: Player.GetPropertyString("media-title"),
            FileName: Player.GetPropertyString("filename/no-ext"),
            HasVideo: hasVideo,
            HasAudio: hasAudio,
            Artist: Player.GetPropertyString("metadata/by-key/artist"),
            Album: Player.GetPropertyString("metadata/by-key/album"),
            TrackNumber: trackNumber,
            Subtitle: AppInfo.Product));

        return new MediaTransportSnapshot(
            IsEnabled: true,
            IsMediaLoaded: true,
            PlaybackStatus: status,
            CanPlay: paused,
            CanPause: !paused && !Player.FileEnded,
            CanStop: !Player.FileEnded,
            CanPrevious: canPrevious,
            CanNext: canNext,
            Metadata: metadata,
            Duration: NormalizeDuration(Player.Duration),
            Position: NormalizePosition(Player.GetPropertyDouble("time-pos", false)));
    }

    static TimeSpan NormalizeDuration(TimeSpan value) =>
        value < TimeSpan.Zero || value == TimeSpan.MaxValue ? TimeSpan.Zero : value;

    static TimeSpan NormalizePosition(double seconds) =>
        double.IsFinite(seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;

    void RunOnUiThread(Action action)
    {
        try
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The player may finish on its event thread while the form is closing.
        }
    }
}
