namespace MpvNet;

internal static class TemporaryFileCleanup
{
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(1);

    public static string DefaultRootFolder =>
        Path.Combine(Folder.LocalAppData, "mpv.net");

    public static string DefaultCacheFolder =>
        Path.Combine(DefaultRootFolder, "Cache");

    public static string DefaultTempFolder =>
        Path.Combine(DefaultRootFolder, "Temp");

    public static void CleanupDefaultFolders() =>
        Cleanup(DateTime.Now, DefaultCacheFolder, DefaultTempFolder);

    internal static void Cleanup(DateTime now, params string[] folders)
    {
        DateTime cutoff = now - Retention;

        foreach (string folder in folders)
            CleanupFolder(folder, cutoff);
    }

    static void CleanupFolder(string folder, DateTime cutoff)
    {
        try
        {
            if (!Directory.Exists(folder))
                return;

            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                DeleteOldFile(file, cutoff);

            foreach (string directory in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories)
                .OrderByDescending(it => it.Length))
            {
                DeleteOldDirectory(directory, cutoff);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to clean temporary folder: {folder}");
        }
    }

    static void DeleteOldFile(string file, DateTime cutoff)
    {
        try
        {
            if (File.GetLastWriteTime(file) < cutoff)
                File.Delete(file);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to delete old temporary file: {file}");
        }
    }

    static void DeleteOldDirectory(string directory, DateTime cutoff)
    {
        try
        {
            if (Directory.GetFileSystemEntries(directory).Length == 0 &&
                Directory.GetLastWriteTime(directory) < cutoff)
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to delete old temporary directory: {directory}");
        }
    }
}
