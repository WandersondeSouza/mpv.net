using System.Threading;
using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed record AudioArtworkRequest(string Path, bool HasAudio, bool HasRealVideo, bool HasEmbeddedCover,
    string? SelectedCoverPath, string VideoSelection, string AudioDisplay, bool AllowAdd,
    bool OnlineEnabled, bool PreferOnline, AudioArtworkIdentity Identity);

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
        bool onlineAllowed = request.OnlineEnabled && request.AllowAdd && request.Identity.Key != null;
        if (onlineAllowed && request.PreferOnline)
        {
            string? preferred = await TryOnlineAsync(request.Identity, token).ConfigureAwait(false);
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
        if (onlineAllowed && !request.PreferOnline)
        {
            string? downloaded = await TryOnlineAsync(request.Identity, token).ConfigureAwait(false);
            if (downloaded != null)
                return new(downloaded, true, "online");
        }
        if (request.AllowAdd && !request.HasEmbeddedCover && (string.IsNullOrEmpty(request.SelectedCoverPath) ||
            MediaTransportThumbnailLoader.IsAvailableLocalFile(request.SelectedCoverPath)))
        {
            using IRandomAccessStream icon = await MediaTransportThumbnailLoader.LoadDefaultIconAsync(token).ConfigureAwait(false);
            using var reader = new DataReader(icon);
            await reader.LoadAsync((uint)icon.Size).AsTask(token).ConfigureAwait(false);
            byte[] bytes = new byte[icon.Size];
            reader.ReadBytes(bytes);
            reader.DetachStream();
            return new(await cache.StoreImageAsync(bytes, token).ConfigureAwait(false), true, "default");
        }
        return null;
    }

    async Task<string?> TryOnlineAsync(AudioArtworkIdentity identity, CancellationToken token)
    {
        try { return await online.FindAsync(identity, token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // No paths, tags or URLs in diagnostics. A transient failure must
            // never stop audio or replace an available local cover.
            Log.Debug($"Online audio artwork unavailable: {ex.GetType().Name}");
            return null;
        }
    }
}
