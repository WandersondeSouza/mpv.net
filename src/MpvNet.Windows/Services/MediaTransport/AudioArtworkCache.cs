using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet.Windows.Services.MediaTransport;

public sealed class AudioArtworkCache(string directory)
{
    public const long MaxCacheBytes = 128L * 1024 * 1024;
    public const int MaxFiles = 512;
    const int MaxEntryBytes = 16 * 1024;
    static readonly SemaphoreSlim Gate = new(1);
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public sealed record Entry(string? ImageHash, DateTimeOffset Expires, ArtistArtworkSource? Artist = null);

    public Task<(bool Found, string? Path)> FindAsync(string key, CancellationToken token) =>
        FindAsync(key, null, token);

    internal async Task<(bool Found, string? Path)> FindAsync(string key,
        Func<ArtistArtworkSource, bool>? validateArtist, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            CheckDirectory();
            string entryPath = Path.Combine(DirectoryPath, Hash(key) + ".json");
            if (!MediaTransportThumbnailLoader.IsAvailableLocalFile(entryPath))
                return (false, null);
            using var stream = new FileStream(entryPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            long length = stream.Length;
            if (length is 0 or > MaxEntryBytes)
                return (false, null);
            byte[] data = new byte[length];
            await stream.ReadExactlyAsync(data, token).ConfigureAwait(false);
            Entry? entry = JsonSerializer.Deserialize<Entry>(data);
            if (entry == null || entry.Expires <= DateTimeOffset.UtcNow)
                return (false, null);
            if (entry.ImageHash == null)
                return (true, null);
            if (entry.ImageHash.Length != 64 || !entry.ImageHash.All(char.IsAsciiHexDigit))
                return (false, null);
            if (validateArtist != null && (entry.Artist == null || !validateArtist(entry.Artist)))
                return (false, null);
            string path = Path.Combine(DirectoryPath, entry.ImageHash + ".png");
            if (await AudioArtworkImage.ReadLocalAsync(path, token, entry.ImageHash).ConfigureAwait(false) == null)
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

    public Task RememberAsync(string key, string? image, TimeSpan validity, CancellationToken token) =>
        RememberAsync(key, image, validity, null, token);

    internal async Task RememberAsync(string key, string? image, TimeSpan validity, ArtistArtworkSource? artist,
        CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureDirectory();
            var entry = new Entry(image == null ? null : Path.GetFileNameWithoutExtension(image), DateTimeOffset.UtcNow + validity, artist);
            string path = Path.Combine(DirectoryPath, Hash(key) + ".json");
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(entry);
            if (data.Length > MaxEntryBytes)
                throw new InvalidDataException("Artwork metadata exceeds the cache limit.");
            await WriteAtomicAsync(path, data, token).ConfigureAwait(false);
            Cleanup(image, path);
        }
        finally { Gate.Release(); }
    }

    void EnsureDirectory()
    {
        CheckDirectory();
        Directory.CreateDirectory(DirectoryPath);
    }

    void CheckDirectory()
    {
        // Check existing parents before creating anything. Artwork must never
        // turn a cache write into a network or placeholder hydration operation.
        string root = Path.GetPathRoot(DirectoryPath)!;
        if (new DriveInfo(root).DriveType != DriveType.Fixed)
            throw new IOException("Artwork cache must be on a fixed drive.");
        string current = root;
        foreach (string part in DirectoryPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (FileNotFoundException) { break; }
            catch (DirectoryNotFoundException) { break; }
            if ((attributes & (FileAttributes.Offline | FileAttributes.ReparsePoint)) != 0)
                throw new IOException("Artwork cache must be a local directory.");
        }
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

    public async Task CleanupAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            CheckDirectory();
            if (Directory.Exists(DirectoryPath))
                Cleanup(null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug($"Artwork cache cleanup unavailable: {ex.GetType().Name}");
        }
        finally { Gate.Release(); }
    }

    void Cleanup(string? preserve, string? entry = null)
    {
        bool Keep(FileInfo file) => StringComparer.OrdinalIgnoreCase.Equals(file.FullName, preserve) ||
            StringComparer.OrdinalIgnoreCase.Equals(file.FullName, entry);

        FileInfo[] files = new DirectoryInfo(DirectoryPath).GetFiles()
            .Where(file => file.Extension is ".png" or ".json" or ".tmp")
            .Where(file => (file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) == 0)
            .OrderByDescending(Keep)
            .ThenByDescending(file => file.LastWriteTimeUtc).ToArray();
        long bytes = 0;
        int count = 0;
        DateTime cutoff = DateTime.UtcNow.AddDays(-30);
        foreach (FileInfo file in files)
        {
            try
            {
                long size = file.Length;
                if (Keep(file) || (bytes + size <= MaxCacheBytes && count < MaxFiles && file.LastWriteTimeUtc >= cutoff))
                {
                    bytes += size;
                    count++;
                }
                else
                    file.Delete();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
