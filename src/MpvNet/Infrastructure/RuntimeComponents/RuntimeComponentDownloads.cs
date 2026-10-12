using System.Security.Cryptography;
using System.Text;

namespace MpvNet;

// Partial transfers outlive the GUID staging generation. Their identity includes
// the published digest, so an updated release never appends to an older asset.
internal static class RuntimeComponentDownloads
{
    static string DownloadsFolder => Path.Combine(RuntimeComponentPaths.TempFolder, "downloads");
    internal static readonly TimeSpan PartialRetention = TimeSpan.FromDays(7);

    public static string GetPartialPath(string url, string digest)
    {
        Directory.CreateDirectory(DownloadsFolder);
        return Path.Combine(DownloadsFolder, GetPartialFileName(url, digest));
    }

    internal static string GetPartialFileName(string url, string digest) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url + "\n" + digest.ToLowerInvariant())))
            .ToLowerInvariant() + ".partial";

    public static void CleanupStalePartials() => CleanupStalePartials(DownloadsFolder, DateTimeOffset.UtcNow);

    internal static void CleanupStalePartials(string directory, DateTimeOffset now)
    {
        if (!Directory.Exists(directory))
            return;
        foreach (string path in Directory.GetFiles(directory, "*.partial", SearchOption.TopDirectoryOnly))
        {
            try
            {
                FileInfo file = new(path);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.Length == 64 && name.All(Uri.IsHexDigit) &&
                    (file.Attributes & FileAttributes.ReparsePoint) == 0 &&
                    new DateTimeOffset(file.LastWriteTimeUtc) <= now - PartialRetention)
                    RuntimeComponentFileSystem.DeleteIfExists(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Debug($"Could not clean partial runtime download. error='{Log.SafeValue(ex.Message)}'");
            }
        }
    }
}
