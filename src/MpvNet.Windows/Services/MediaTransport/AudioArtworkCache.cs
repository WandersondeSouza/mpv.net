using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed class AudioArtworkCache(string directory)
{
    public const long MaxCacheBytes = 128L * 1024 * 1024;
    public const int MaxEntries = 256;
    static readonly SemaphoreSlim Gate = new(1);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public sealed record Entry(string? ImageHash, DateTimeOffset Expires);

    public async Task<(bool Found, string? Path)> FindAsync(string key, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            string entryPath = Path.Combine(DirectoryPath, Hash(key) + ".json");
            if (!MediaTransportThumbnailLoader.IsAvailableLocalFile(entryPath) || new FileInfo(entryPath).Length > 1024)
                return (false, null);
            Entry? entry = JsonSerializer.Deserialize<Entry>(await File.ReadAllTextAsync(entryPath, token).ConfigureAwait(false));
            if (entry == null || entry.Expires <= DateTimeOffset.UtcNow)
                return (false, null);
            if (entry.ImageHash == null)
                return (true, null);
            if (entry.ImageHash.Length != 64 || !entry.ImageHash.All(char.IsAsciiHexDigit))
                return (false, null);
            string path = Path.Combine(DirectoryPath, entry.ImageHash + ".png");
            if (await AudioArtworkImage.ReadLocalAsync(path, token).ConfigureAwait(false) == null)
                return (false, null);
            return (true, path);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return (false, null);
        }
        finally { Gate.Release(); }
    }

    public async Task<string> StoreImageAsync(byte[] png, CancellationToken token)
    {
        if (png.Length is 0 or > AudioArtworkImage.MaxBytes)
            throw new InvalidDataException("Artwork exceeds the cache limit.");
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureDirectory();
            string path = Path.Combine(DirectoryPath, Convert.ToHexString(SHA256.HashData(png)) + ".png");
            await WriteAtomicAsync(path, png, token).ConfigureAwait(false);
            Cleanup(path);
            return path;
        }
        finally { Gate.Release(); }
    }

    public async Task RememberAsync(string key, string? image, TimeSpan validity, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureDirectory();
            var entry = new Entry(image == null ? null : Path.GetFileNameWithoutExtension(image), DateTimeOffset.UtcNow + validity);
            string path = Path.Combine(DirectoryPath, Hash(key) + ".json");
            await WriteAtomicAsync(path, JsonSerializer.SerializeToUtf8Bytes(entry), token).ConfigureAwait(false);
            Cleanup(image);
        }
        finally { Gate.Release(); }
    }

    void EnsureDirectory()
    {
        // Use the same local/no-placeholder policy as the thumbnail reader,
        // including the ancestors, before writing any cache content.
        // Check parents without creating a probe in an untrusted destination.
        string? parent = DirectoryPath;
        while (parent != null)
        {
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & (FileAttributes.Offline | FileAttributes.ReparsePoint)) != 0)
                throw new IOException("Artwork cache must be a local directory.");
            parent = Path.GetDirectoryName(parent);
        }
        if (new DriveInfo(Path.GetPathRoot(DirectoryPath)!).DriveType != DriveType.Fixed)
            throw new IOException("Artwork cache must be on a fixed drive.");
        Directory.CreateDirectory(DirectoryPath);
    }

    static async Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken token)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temp, path, true);
        }
        finally { File.Delete(temp); }
    }

    void Cleanup(string? preserve)
    {
        FileInfo[] files = new DirectoryInfo(DirectoryPath).GetFiles()
            .Where(file => file.Extension is ".png" or ".json" or ".tmp")
            .OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
        long bytes = 0;
        int count = 0;
        foreach (FileInfo file in files)
        {
            bytes += file.Length;
            count++;
            if (StringComparer.OrdinalIgnoreCase.Equals(file.FullName, preserve))
                continue;
            if (bytes <= MaxCacheBytes && count <= MaxEntries * 2 && file.LastWriteTimeUtc >= DateTime.UtcNow.AddDays(-30))
                continue;
            try { file.Delete(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
