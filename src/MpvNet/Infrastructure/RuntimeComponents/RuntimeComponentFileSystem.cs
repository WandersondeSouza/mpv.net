using System.Security.Cryptography;

namespace MpvNet;

internal static class RuntimeComponentFileSystem
{
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
