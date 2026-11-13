using System.Threading;
using System.Threading.Tasks;

using MpvNet.Windows.Services.MediaTransport;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    readonly AudioArtworkCache _audioArtworkCache;
    readonly Task _audioArtworkCacheCleanup;
    OnlineAudioArtwork? _onlineAudioArtwork;
    AudioArtworkRequest? _audioArtworkRequest;
    (CancellationTokenSource Cancellation, Task Work)? _audioArtworkOperation;
    readonly List<Task> _audioArtworkRetirements = [];
    string? _audioArtworkPath;
    bool _audioArtworkPlaybackReady;
    HashSet<string>? _artworkExplicitOptions;

    void ResetAudioArtwork()
    {
        _audioArtworkPlaybackReady = false;
        CancelAudioArtwork();
        _audioArtworkPath = null;
        _audioArtworkRequest = null;
        Player.SetPropertyString("user-data/mpvnet/audio-artwork-source", "none");
    }

    void CancelAudioArtwork()
    {
        if (_audioArtworkOperation is not { } operation)
            return;

        _audioArtworkOperation = null;
        _audioArtworkRetirements.RemoveAll(task => task.IsCompleted);
        _audioArtworkRetirements.Add(FinishAudioArtworkAsync(operation.Cancellation, operation.Work));
    }

    static async Task FinishAudioArtworkAsync(CancellationTokenSource cancellation, Task work)
    {
        using (cancellation)
        {
            try
            {
                await Task.WhenAll(cancellation.CancelAsync(), work).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Audio artwork cancellation failed.");
            }
        }
    }

    void DisposeAudioArtwork()
    {
        CancelAudioArtwork();
        try
        {
            // Workers post UI updates without waiting for the window to process them.
            Task.WhenAll(_audioArtworkRetirements.Append(_audioArtworkCacheCleanup)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Audio artwork cleanup failed.");
        }
        finally
        {
            _audioArtworkRetirements.Clear();
            _onlineAudioArtwork?.Dispose();
            _onlineAudioArtwork = null;
        }
    }

    MediaTransportSnapshot ResolveAudioArtwork(MediaTransportSnapshot snapshot)
    {
        if (!_audioArtworkPlaybackReady)
            return snapshot;
        if (snapshot.Metadata is not { MediaType: MediaTransportMediaType.Music } metadata)
        {
            CancelAudioArtwork();
            _audioArtworkPath = null;
            _audioArtworkRequest = null;
            return snapshot;
        }
        bool embedded = Player.GetPropertyBool("current-tracks/video/albumart", false) &&
            !Player.GetPropertyBool("current-tracks/video/external", false);
        string selectedVideo = Player.GetPropertyString("vid");
        _artworkExplicitOptions ??= MpvOptionConfiguration.GetExplicitOptions();
        bool explicitChoice = _artworkExplicitOptions.Contains("*") || new[] {
            "vid", "video", "audio-display", "cover-art-auto", "cover-art-file", "cover-art-files",
            "cover-art-files-append", "external-file", "external-files", "track-auto-selection", "autoload-files"
        }.Any(_artworkExplicitOptions.Contains);
        // vid reports "no" when audio simply has no picture. options/vid
        // distinguishes that from the user's explicit decision to disable it.
        bool canAdd = !explicitChoice && Player.GetPropertyString("options/vid") == "auto" &&
            Player.GetPropertyBool("track-auto-selection", false) && Player.GetPropertyBool("autoload-files", false) &&
            Player.GetPropertyString("cover-art-auto") != "no";
        var identity = new AudioArtworkIdentity(metadata.AlbumArtist ?? metadata.Artist, metadata.Album,
            ReadAudioArtworkTag("musicbrainz_albumid", "MusicBrainz Album Id"),
            ReadAudioArtworkTag("musicbrainz_releasegroupid", "MusicBrainz Release Group Id"));
        var request = new AudioArtworkRequest(Player.GetPropertyString("path"), true, false, embedded,
            Player.GetPropertyBool("current-tracks/video/albumart", false)
                ? Player.GetPropertyString("current-tracks/video/external-filename") : null,
            selectedVideo, Player.GetPropertyString("audio-display"), canAdd,
            App.AudioArtworkOnline, App.AudioArtworkPreferOnline, identity,
            new AudioArtistIdentity(ReadAudioArtworkTag("artist") ?? ReadAudioArtworkTag("album_artist", "albumartist"),
                ReadAudioArtworkTag("musicbrainz_artistid", "MusicBrainz Artist Id")));

        if (_audioArtworkPath != null && StringComparer.OrdinalIgnoreCase.Equals(request.SelectedCoverPath, _audioArtworkPath))
        {
            _audioArtworkRequest = request;
            return snapshot with { Metadata = metadata with { ThumbnailPath = _audioArtworkPath } };
        }

        if (request != _audioArtworkRequest)
        {
            CancelAudioArtwork();
            _audioArtworkRequest = request;
            _audioArtworkPath = null;
            Player.SetPropertyString("user-data/mpvnet/audio-artwork-source", "none");
            long generation = Interlocked.Read(ref _mediaTransportGeneration);
            _onlineAudioArtwork ??= new(_audioArtworkCache);
            var resolver = new AudioArtworkResolver(_audioArtworkCache, _onlineAudioArtwork);
            var cancellationSource = new CancellationTokenSource();
            CancellationToken token = cancellationSource.Token;
            Task work = Task.Run(async () =>
            {
                try
                {
                    await _audioArtworkCacheCleanup.WaitAsync(token).ConfigureAwait(false);
                    AudioArtworkResult? result = await resolver.ResolveAsync(request, cancellation =>
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (generation != Interlocked.Read(ref _mediaTransportGeneration))
                            return Task.FromResult<AlbumArtworkPixels?>(null);
                        AlbumArtworkPixels? pixels = Player.CaptureAlbumArtwork();
                        cancellation.ThrowIfCancellationRequested();
                        if (generation != Interlocked.Read(ref _mediaTransportGeneration) ||
                            Player.GetPropertyString("path") != request.Path || Player.GetPropertyString("vid") != request.VideoSelection)
                            return Task.FromResult<AlbumArtworkPixels?>(null);
                        return Task.FromResult(pixels);
                    }, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    BeginInvoke(() =>
                    {
                        if (result == null || token.IsCancellationRequested || _closingInput || _managedResourcesDisposed ||
                            generation != Interlocked.Read(ref _mediaTransportGeneration) || request != _audioArtworkRequest ||
                            Player.GetPropertyString("path") != request.Path || Player.GetPropertyString("vid") != request.VideoSelection ||
                            Player.GetPropertyString("audio-display") != request.AudioDisplay)
                            return;
                        _audioArtworkPath = result.Path;
                        Player.SetPropertyString("user-data/mpvnet/audio-artwork-source", result.Source);
                        if (result.AddToPlayer && request.AllowAdd)
                            Player.AddAlbumArtwork(result.Path);
                        UpdateMediaTransport();
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Log.Debug($"Audio artwork unavailable: {ex.GetType().Name}"); }
            });
            _audioArtworkOperation = (cancellationSource, work);
        }
        return _audioArtworkPath == null ? snapshot : snapshot with { Metadata = metadata with { ThumbnailPath = _audioArtworkPath } };
    }

    string? ReadAudioArtworkTag(params string[] names)
    {
        foreach (string name in names)
        {
            string value = Player.GetPropertyString("metadata/by-key/" + name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }
}
