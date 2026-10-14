using System.Threading;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace MpvNet.Windows.Services.MediaTransport;

public static class MediaTransportThumbnailLoader
{
    public static async Task<IRandomAccessStream> LoadDefaultIconAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using Stream iconResource = typeof(MediaTransportThumbnailLoader).Assembly
            .GetManifestResourceStream("MpvNet.Windows.DefaultIcon.ico")
            ?? throw new InvalidOperationException("Application icon resource is missing.");
        using var icon = new System.Drawing.Icon(iconResource, 256, 256);
        using var bitmap = icon.ToBitmap();
        using var png = new MemoryStream();
        bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);

        var stream = new InMemoryRandomAccessStream();
        try
        {
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(png.ToArray());
                await writer.StoreAsync().AsTask(token).ConfigureAwait(false);
                writer.DetachStream();
            }
            stream.Seek(0);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public static async Task<IRandomAccessStream?> LoadAsync(MediaTransportMetadata metadata, CancellationToken token)
    {
        string? path = MediaTransportMetadataBuilder.GetLocalMediaPath(metadata.ThumbnailPath ?? metadata.LocalMediaPath);
        if (path == null)
            return null;

        token.ThrowIfCancellationRequested();
        // Refuse network drives and offline/cloud placeholders; never hydrate for artwork.
        if (!IsAvailableLocalFile(path))
            return null;

        IRandomAccessStream? stream = null;
        IRandomAccessStream? source = null;
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path).AsTask(token).ConfigureAwait(false);
            if (metadata.ThumbnailPath != null)
                source = await file.OpenReadAsync().AsTask(token).ConfigureAwait(false);
            else
            {
                StorageItemThumbnail? thumbnail = await file.GetThumbnailAsync(
                    metadata.MediaType == MediaTransportMediaType.Music ? ThumbnailMode.MusicView : ThumbnailMode.VideosView,
                    256, ThumbnailOptions.ReturnOnlyIfCached).AsTask(token).ConfigureAwait(false);
                source = thumbnail;
                if (thumbnail == null || thumbnail.Type != ThumbnailType.Image)
                    return null;
            }

            if (source.Size == 0 || source.Size > 4 * 1024 * 1024)
                return null;

            // Decode/publish an owned memory copy so Windows never keeps the media
            // or image file mapped after we release its source stream.
            stream = new InMemoryRandomAccessStream();
            await RandomAccessStream.CopyAsync(source, stream, 4 * 1024 * 1024 + 1).AsTask(token).ConfigureAwait(false);
            if (stream.Size > 4 * 1024 * 1024)
                return null;
            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token).ConfigureAwait(false);
            if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 ||
                decoder.PixelWidth > 4096 || decoder.PixelHeight > 4096)
                return null;

            token.ThrowIfCancellationRequested();
            stream.Seek(0);
            IRandomAccessStream result = stream;
            stream = null;
            return result;
        }
        finally
        {
            stream?.Dispose();
            source?.Dispose();
        }
    }

    static bool IsAvailableLocalFile(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath)!;
            if (new DriveInfo(root).DriveType != DriveType.Fixed)
                return false;

            // Check parents before children, so a junction cannot turn a local
            // filename into a network request or a cloud hydration operation.
            string current = root;
            foreach (string part in fullPath[root.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if ((File.GetAttributes(current) & (System.IO.FileAttributes.Offline | System.IO.FileAttributes.ReparsePoint)) != 0)
                    return false;
            }
            return File.Exists(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
