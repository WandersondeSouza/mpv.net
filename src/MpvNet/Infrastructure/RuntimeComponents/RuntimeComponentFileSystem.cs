using System.Security.Cryptography;

namespace MpvNet;

internal static class RuntimeComponentFileSystem
{
    public static RuntimeComponentFileFingerprint GetFileFingerprint(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists)
            throw new FileNotFoundException("Runtime component file was not found.", path);

        return new RuntimeComponentFileFingerprint(file.Length, file.LastWriteTimeUtc.Ticks);
    }

    public static bool HasMatchingFileFingerprint(string path, RuntimeComponentFileFingerprint fingerprint) =>
        GetFileFingerprint(path) == fingerprint;

    public static (string Digest, RuntimeComponentFileFingerprint Fingerprint) GetFileDigestAndFingerprint(string path)
    {
        RuntimeComponentFileFingerprint before = GetFileFingerprint(path);
        string digest = GetFileDigest(path);
        RuntimeComponentFileFingerprint after = GetFileFingerprint(path);
        if (before != after)
            throw new IOException("Runtime component changed while its SHA-256 was being verified.");

        return (digest, after);
    }

    public static string GetFileDigest(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static bool IsFileLocked(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            else if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to delete temporary runtime component path. path='{Log.SafeValue(path)}'");
        }
    }
}
