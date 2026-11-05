using System.Threading;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MpvNet.Windows.Services.MediaTransport;

public static class AudioArtworkImage
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public const int MaxDimension = 4096;

    public static async Task<byte[]> NormalizeAsync(byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (bytes.Length is 0 or > MaxBytes)
            throw new InvalidDataException("Artwork size exceeds the limit.");
        using var input = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(input))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync().AsTask(token).ConfigureAwait(false);
            writer.DetachStream();
        }
        input.Seek(0);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input).AsTask(token).ConfigureAwait(false);
        if (decoder.PixelWidth is 0 or > MaxDimension || decoder.PixelHeight is 0 or > MaxDimension)
            throw new InvalidDataException("Artwork dimensions exceed the limit.");
        // Decode the pixels, not only the header. Re-encode as PNG, stripping
        // metadata and preventing a truncated/foreign payload reaching mpv.
        PixelDataProvider pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight, new BitmapTransform(), ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(token).ConfigureAwait(false);
        return await EncodeAsync(new((int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight,
            pixels.DetachPixelData()), token).ConfigureAwait(false);
    }

    public static async Task<byte[]> EncodeAsync(AlbumArtworkPixels pixels, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (pixels.Width is <= 0 or > MaxDimension || pixels.Height is <= 0 or > MaxDimension ||
            pixels.Bgra.Length != (long)pixels.Width * pixels.Height * 4)
            throw new InvalidDataException("Invalid artwork pixels.");
        using var output = new InMemoryRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask(token).ConfigureAwait(false);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
            (uint)pixels.Width, (uint)pixels.Height, 96, 96, pixels.Bgra);
        await encoder.FlushAsync().AsTask(token).ConfigureAwait(false);
        if (output.Size is 0 or > MaxBytes)
            throw new InvalidDataException("Encoded artwork exceeds the limit.");
        output.Seek(0);
        using var reader = new DataReader(output);
        await reader.LoadAsync((uint)output.Size).AsTask(token).ConfigureAwait(false);
        byte[] bytes = new byte[output.Size];
        reader.ReadBytes(bytes);
        reader.DetachStream();
        token.ThrowIfCancellationRequested();
        return bytes;
    }

    public static async Task<byte[]?> ReadLocalAsync(string? path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (path == null || !MediaTransportThumbnailLoader.IsAvailableLocalFile(path))
            return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            8192, FileOptions.Asynchronous);
        if (stream.Length is 0 or > MaxBytes)
            return null;
        byte[] bytes = new byte[stream.Length];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return await NormalizeAsync(bytes, token).ConfigureAwait(false);
    }
}
