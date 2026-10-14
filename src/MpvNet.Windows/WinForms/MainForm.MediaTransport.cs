using System.Globalization;
using System.Threading;

using MpvNet;
using MpvNet.Native;
using MpvNet.Windows.Services.MediaTransport;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    long _mediaTransportGeneration;

    void Player_StartFile()
    {
        _videoClick.Invalidate();
        Interlocked.Increment(ref _mediaTransportGeneration);
        _mediaTransportMediaLoaded = false;
        RunOnUiThread(UpdateMediaTransport);
    }

    void Player_EndFile(LibMpv.mpv_end_file_reason reason)
    {
        _videoClick.Invalidate();
        Interlocked.Increment(ref _mediaTransportGeneration);
        _mediaTransportMediaLoaded = false;
        RunOnUiThread(UpdateMediaTransport);
    }

    void Player_Seek() => UpdateMediaTransport();

    void Player_PlaybackRestart() => UpdateMediaTransport();

    void MediaTransportTimer_Tick(object? sender, EventArgs e) => UpdateMediaTransport();

    void HandleMediaTransportCommand(MediaTransportCommandEventArgs args)
    {
        if (InvokeRequired)
        {
            long generation = Interlocked.Read(ref _mediaTransportGeneration);
            RunOnUiThread(() =>
            {
                if (generation == Interlocked.Read(ref _mediaTransportGeneration))
                    HandleMediaTransportCommand(args);
            });
            return;
        }

        if (_managedResourcesDisposed || _closingInput || !_mediaTransportMediaLoaded)
            return;

        Log.Debug($"Media transport command: {args.Command}");

        switch (args.Command)
        {
            case MediaTransportCommand.Play:
                Player.CommandV("set", "pause", "no");
                break;
            case MediaTransportCommand.Pause:
                Player.CommandV("set", "pause", "yes");
                break;
            case MediaTransportCommand.Stop:
                Player.CancelLiveStreamRecovery();
                Player.CommandV("stop");
                break;
            case MediaTransportCommand.Next:
                Player.CommandV("playlist-next", "force");
                break;
            case MediaTransportCommand.Previous:
                Player.CommandV("playlist-prev", "force");
                break;
            case MediaTransportCommand.Seek:
            case MediaTransportCommand.FastForward:
            case MediaTransportCommand.Rewind:
                if (MediaTransportController.GetSeekPosition(BuildMediaTransportSnapshot(), args) is not TimeSpan position)
                    break;

                // Use the current mpv position for relative jumps, clamped to the valid range.
                // PlaybackRestart publishes the actual seek result.
                Player.CommandV(
                    "seek",
                    position.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                    "absolute+exact");
                break;
        }
    }

    void UpdateMediaTransport()
    {
        if (_mediaTransport == null || _managedResourcesDisposed || _closingInput)
            return;

        if (InvokeRequired)
        {
            RunOnUiThread(UpdateMediaTransport);
            return;
        }

        MediaTransportSnapshot snapshot = BuildMediaTransportSnapshot();
        _mediaTransport.Publish(snapshot);
        UpdateTaskbarThumbnail(snapshot);

        if (_mediaTransport.IsAvailable && snapshot.IsEnabled && snapshot.PlaybackStatus == MediaTransportPlaybackStatus.Playing)
            _mediaTransportTimer?.Start();
        else
            _mediaTransportTimer?.Stop();
    }

    MediaTransportSnapshot BuildMediaTransportSnapshot()
    {
        string path = Player.GetPropertyString("path");
        bool mediaLoaded = _mediaTransportMediaLoaded && !string.IsNullOrWhiteSpace(path);

        if (!mediaLoaded)
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

        bool albumArt = Player.GetPropertyBool("current-tracks/video/albumart", false);
        bool hasVideo = !albumArt && (FileTypes.IsVideoFile(path) || !string.IsNullOrWhiteSpace(Player.GetPropertyString("video-codec")));
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
            Subtitle: AppInfo.Product,
            AlbumArtist: Player.GetPropertyString("metadata/by-key/album_artist") is { Length: > 0 } albumArtist
                ? albumArtist : Player.GetPropertyString("metadata/by-key/albumartist"),
            ThumbnailPath: albumArt ? Player.GetPropertyString("current-tracks/video/external-filename") : null));

        double durationSeconds = Player.GetPropertyDouble("duration", false);
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
            Duration: MediaTransportSnapshot.FromSeconds(durationSeconds),
            Position: MediaTransportSnapshot.FromSeconds(Player.GetPropertyDouble("time-pos", false)),
            CanSeek: MediaTransportSnapshot.SupportsSeeking(
                Player.GetPropertyBool("seekable", false),
                Player.GetPropertyBool("partially-seekable", false), durationSeconds));
    }

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
