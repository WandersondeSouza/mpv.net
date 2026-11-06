using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed record AudioArtworkRequest(string Path, bool HasAudio, bool HasRealVideo, bool HasEmbeddedCover,
    string? SelectedCoverPath, string VideoSelection, string AudioDisplay, bool AllowAdd,
    bool OnlineEnabled, bool PreferOnline, AudioArtworkIdentity Identity, AudioArtistIdentity? Artist = null);

public sealed record AudioArtworkResult(string Path, bool AddToPlayer, string Source = "local");

public sealed class AudioArtworkResolver(AudioArtworkCache cache, OnlineAudioArtwork online)
{
    public static bool IsEligible(AudioArtworkRequest request) => request.HasAudio && !request.HasRealVideo &&
        request.AudioDisplay != "no" && MediaTransportMetadataBuilder.GetLocalMediaPath(request.Path) != null;

    public async Task<AudioArtworkResult?> ResolveAsync(AudioArtworkRequest request,
        Func<CancellationToken, Task<AlbumArtworkPixels?>> capture, CancellationToken token)
    {
        if (!IsEligible(request) || !MediaTransportThumbnailLoader.IsAvailableLocalFile(request.Path))
            return null;
        if (request.AllowAdd && request.PreferOnline)
        {
            string? preferred = await online.FindAsync(request.Identity, request.OnlineEnabled, token).ConfigureAwait(false);
            if (preferred != null)
                return new(preferred, true, "online");
        }
        try
        {
            byte[]? local = await AudioArtworkImage.ReadLocalAsync(request.SelectedCoverPath, token).ConfigureAwait(false);
            if (local == null && request.HasEmbeddedCover)
            {
                AlbumArtworkPixels? pixels = await capture(token).ConfigureAwait(false);
                if (pixels != null)
                    local = await AudioArtworkImage.EncodeAsync(pixels, token).ConfigureAwait(false);
            }
            if (local != null)
                return new(await cache.StoreImageAsync(local, token).ConfigureAwait(false), false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Log.Debug($"Local audio artwork unavailable: {ex.GetType().Name}"); }
        if (request.AllowAdd && !request.PreferOnline)
        {
            string? downloaded = await online.FindAsync(request.Identity, request.OnlineEnabled, token).ConfigureAwait(false);
            if (downloaded != null)
                return new(downloaded, true, "online");
        }
        if (request.AllowAdd && !request.HasEmbeddedCover && (string.IsNullOrEmpty(request.SelectedCoverPath) ||
            MediaTransportThumbnailLoader.IsAvailableLocalFile(request.SelectedCoverPath)))
        {
            if (request.Artist is { } artist)
            {
                string? photo = await online.FindArtistAsync(artist, request.OnlineEnabled, token).ConfigureAwait(false);
                if (photo != null)
                    return new(photo, true, "artist");
            }
            token.ThrowIfCancellationRequested();
            return new(await cache.StoreImageAsync(AudioArtworkImage.CreateDefault(), token).ConfigureAwait(false), true, "default");
        }
        return null;
    }

}
