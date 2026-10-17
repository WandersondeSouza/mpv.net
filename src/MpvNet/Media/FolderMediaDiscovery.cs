using System.Threading;

using MpvNet.Extensions;
using MpvNet.Native;

namespace MpvNet;

internal enum FolderMediaKind { Collection, Dvd, BluRay }

internal sealed record FolderMediaSelection(
    string Root, FolderMediaKind Kind, List<PlaylistFileItem> Items,
    bool RootAccessible, int Failures, int SkippedEntries);

// The small filesystem seam lets tests reproduce a drive disappearing or an ACL
// failure without changing permissions on the user's machine.
internal class FolderMediaFileSystem
{
    public virtual string[] GetEntries(string path) => Directory.GetFileSystemEntries(path);
    public virtual FileAttributes GetAttributes(string path) => File.GetAttributes(path);
}

internal static class FolderMediaDiscovery
{
    public static FolderMediaSelection Discover(string path, CancellationToken cancellationToken,
        FolderMediaFileSystem? fileSystem = null)
    {
        fileSystem ??= new();
        string root = path;
        int failures = 0, skipped = 0;
        bool rootAccessible = false;
        List<string> files = [];
        List<(string Path, string Category)> diagnostics = [];
        try
        {
            root = Path.GetFullPath(path);
            FileAttributes attributes = fileSystem.GetAttributes(root);
            if (!attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
                return Result();

            FolderMediaKind kind = Classify(root, out string deviceRoot);
            if (kind != FolderMediaKind.Collection)
                return new(deviceRoot, kind, [], true, 0, 0);

            Stack<string> pending = new();
            pending.Push(root);
            while (pending.TryPop(out string? directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Recheck after enumeration: a directory can disappear or become a link.
                    if (fileSystem.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint) ||
                        IsOpticalDirectoryName(directory) || Classify(directory, out string ignoredDeviceRoot) != FolderMediaKind.Collection)
                    {
                        skipped++;
                        continue;
                    }

                    string[] entries = fileSystem.GetEntries(directory);
                    if (directory == root)
                        rootAccessible = true;
                    foreach (string entry in entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            FileAttributes entryAttributes = fileSystem.GetAttributes(entry);
                            if (entryAttributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                skipped++;
                                continue;
                            }
                            if (entryAttributes.HasFlag(FileAttributes.Directory))
                                pending.Push(entry);
                            else
                                files.Add(entry);
                        }
                        catch (Exception ex) when (IsFileSystemFailure(ex)) { Record(entry, ex); }
                    }
                }
                catch (Exception ex) when (IsFileSystemFailure(ex)) { Record(directory, ex); }
            }
        }
        catch (Exception ex) when (IsFileSystemFailure(ex)) { Record(root, ex); }

        IComparer<string> natural = new StringLogicalComparer();
        int ComparePaths(string left, string right)
        {
            int comparison = natural.Compare(left, right);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left, right);
        }

        files = FileTypes.GetMediaFiles(files.ToArray()).ToList();
        files.Sort((left, right) => ComparePaths(Relative(left), Relative(right)));
        List<(PlaylistFileItem Item, string Order, int Index)> candidates = [];
        Dictionary<string, string> playlistTitles = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FileTypes.IsPlaylist(file.Ext()))
            {
                int index = 0;
                foreach (PlaylistFileItem item in PlaylistFile.Read(file, ex => Record(file, ex)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!FileTypes.IsStreamingUrl(item.Path) && !IsSafePlaylistFile(item.Path))
                        continue;
                    playlistTitles.TryAdd(PlaylistFile.GetAddressKey(item.Path), item.Title);
                    candidates.Add((item, FileTypes.IsStreamingUrl(item.Path) ? Relative(file) : Relative(item.Path), index++));
                }
            }
            else
                candidates.Add((new(file, ""), Relative(file), 0));
        }
        candidates.Sort((left, right) =>
        {
            int comparison = ComparePaths(left.Order, right.Order);
            return comparison != 0 ? comparison : left.Index.CompareTo(right.Index);
        });
        List<PlaylistFileItem> items = PlaylistFile.PrepareForPlayback(candidates.Select(candidate =>
            playlistTitles.TryGetValue(PlaylistFile.GetAddressKey(candidate.Item.Path), out string? title)
                ? candidate.Item with { Title = title } : candidate.Item));
        return Result(items);

        string Relative(string file) => Path.GetRelativePath(root, file);
        FolderMediaSelection Result(List<PlaylistFileItem>? items = null)
        {
            if (failures > 0)
                Log.Error($"Folder discovery encountered failures. count={failures}, examples={string.Join("; ", diagnostics.Select(d => $"{Log.SafeValue(d.Path)} ({d.Category})"))}");
            return new(root, FolderMediaKind.Collection, items ?? [], rootAccessible, failures, skipped);
        }
        void Record(string file, Exception ex)
        {
            failures++;
            if (diagnostics.Count < 5)
                diagnostics.Add((file, ex.GetType().Name));
        }
        bool IsSafePlaylistFile(string file)
        {
            try
            {
                FileAttributes attributes = fileSystem.GetAttributes(file);
                if (attributes.HasFlag(FileAttributes.Directory) || attributes.HasFlag(FileAttributes.ReparsePoint))
                    return false;
                for (string? parent = Path.GetDirectoryName(file); parent != null; parent = Path.GetDirectoryName(parent))
                {
                    if (IsOpticalDirectoryName(parent) ||
                        fileSystem.GetAttributes(parent).HasFlag(FileAttributes.ReparsePoint) ||
                        Classify(parent, out string ignoredParentDeviceRoot) != FolderMediaKind.Collection)
                        return false;
                }
                return true;
            }
            catch (Exception ex) when (IsFileSystemFailure(ex)) { Record(file, ex); return false; }
        }
    }

    internal static FolderMediaKind Classify(string path, out string deviceRoot)
    {
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        deviceRoot = path;
        string name = Path.GetFileName(path);
        if (name.Equals("BDMV", StringComparison.OrdinalIgnoreCase) && HasMarker(path, "index.bdmv"))
        {
            deviceRoot = Path.GetDirectoryName(path)!;
            return FolderMediaKind.BluRay;
        }
        if (HasMarker(Path.Combine(path, "BDMV"), "index.bdmv"))
            return FolderMediaKind.BluRay;
        if (name.Equals("VIDEO_TS", StringComparison.OrdinalIgnoreCase) && HasMarker(path, "VIDEO_TS.IFO"))
        {
            deviceRoot = Path.GetDirectoryName(path)!;
            return FolderMediaKind.Dvd;
        }
        return HasMarker(Path.Combine(path, "VIDEO_TS"), "VIDEO_TS.IFO")
            ? FolderMediaKind.Dvd : FolderMediaKind.Collection;
    }

    static bool HasMarker(string directory, string marker)
    {
        try
        {
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
                return false;
            FileAttributes attributes = File.GetAttributes(Path.Combine(directory, marker));
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        }
        catch (Exception ex) when (IsFileSystemFailure(ex)) { return false; }
    }

    static bool IsOpticalDirectoryName(string path) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(path)).Equals("BDMV", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(Path.TrimEndingDirectorySeparator(path)).Equals("VIDEO_TS", StringComparison.OrdinalIgnoreCase);

    static bool IsFileSystemFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException;
}
