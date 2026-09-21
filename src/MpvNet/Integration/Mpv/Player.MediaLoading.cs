using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.Native;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer
{
    public const string LoadfileOptionsInsertionIndex = "-1";
    public const string AutomaticStreamingLoadOptions = NetworkCachePolicy.BalancedHttpOptions;

    public void SetBluRayTitle(int id) => LoadFiles(new[] { @"bd://" + id }, false, false, source: MediaInputSource.InternalCommand);

    public DateTime LastLoad;

    public void LoadFiles(
        string[]? files,
        bool loadFolder,
        bool append,
        string? fallbackInput = null,
        MediaInputSource source = MediaInputSource.Unknown)
    {
        IReadOnlyList<MediaLoadRequest> requests = MediaInputNormalizer.NormalizeMany(files, source, append);
        if (requests.Count == 0)
        {
            Log.Debug($"LoadFiles skipped because no files were supplied. loadFolder={loadFolder}, append={append}");
            return;
        }

        bool replaceRequested = !append;

        if ((DateTime.Now - LastLoad).TotalMilliseconds < 1000)
        {
            Log.Debug("LoadFiles called within 1000 ms of previous load; forcing append mode.");
            append = true;
        }

        if (replaceRequested)
            BeginNewMediaLoad();

        LastLoad = DateTime.Now;
        Log.Debug($"Loading media inputs. count={requests.Count}, source={source}, loadFolder={loadFolder}, append={append}, fallback='{Log.SafeValue(fallbackInput)}', inputs={Log.SafeValues(requests.Select(request => request.Input))}");

        ArmAutoLoadFolder(loadFolder && !append);

        for (int i = 0; i < requests.Count; i++)
        {
            MediaLoadRequest request = requests[i];
            string file = request.Input;

            string originalFile = file;
            file = ConvertFilePath(file);
            Log.Debug($"Prepared media input at index {i}: original='{Log.SafeValue(originalFile)}', converted='{Log.SafeValue(file)}'");

            string ext = file.Ext();
            Log.Debug($"Media input extension classified. index={i}, extension='{ext}', path='{Log.SafeValue(file)}'");

            if (OperatingSystem.IsWindows())
            {
                switch (ext)
                {
                    case "avs":
                        Log.Debug("Loading AviSynth support for .avs input.");
                        LoadAviSynth();
                        break;
                    case "lnk":
                        string shortcutTarget = GetShortcutTarget(file);
                        Log.Debug($"Resolved shortcut target. shortcut='{Log.SafeValue(file)}', target='{Log.SafeValue(shortcutTarget)}'");
                        file = shortcutTarget;
                        break;
                }
            }

            if (FileTypes.IsPlaylist(ext) && File.Exists(file))
            {
                bool appendPlaylist = append || i > 0 || !string.IsNullOrEmpty(GetPropertyString("path"));

                try
                {
                    var playlistItems = PlaylistFile.Read(file);
                    List<PlaylistFileItem> itemsToLoad = [];
                    Log.Debug($"Playlist file expanded. path='{Log.SafeValue(file)}', parsedItems={playlistItems.Count}, appendPlaylist={appendPlaylist}");

                    foreach (var item in playlistItems)
                    {
                        if (PlaylistContainsPath(item.Path))
                        {
                            Log.Debug($"Skipping playlist duplicate item: '{Log.SafeValue(item.Path)}'");
                            continue;
                        }

                        itemsToLoad.Add(item);
                    }

                    if (itemsToLoad.Count > 0)
                    {
                        Log.Debug($"Loading playlist items. playlist='{Log.SafeValue(file)}', count={itemsToLoad.Count}, append={appendPlaylist}");
                        LoadPlaylistItems(itemsToLoad, appendPlaylist);
                    }
                    else
                    {
                        Log.Debug($"Playlist file did not add new items. path='{Log.SafeValue(file)}'");

                        if (string.IsNullOrEmpty(GetPropertyString("path")))
                            TryLoadFallbackDirect(fallbackInput, file, appendPlaylist, "empty playlist expansion");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Playlist expansion failed; playback fallback will be attempted. playlist='{Log.SafeValue(file)}', fallback='{Log.SafeValue(fallbackInput)}'");

                    if (!TryLoadFallbackDirect(fallbackInput, file, appendPlaylist, "playlist expansion failure"))
                    {
                        Log.Debug($"Falling back to raw playlist file through mpv. playlist='{Log.SafeValue(file)}'");
                        SendLoadfile(file, i, append);
                    }
                }
            }
            else if (ext == "iso")
            {
                Log.Debug($"Loading ISO media input: '{Log.SafeValue(file)}'");
                LoadISO(file);
            }
            else if(FileTypes.Subtitle.Contains(ext))
            {
                Log.Debug($"Adding subtitle from media input: '{Log.SafeValue(file)}'");
                CommandV("sub-add", file);
            }
            else
            {
                SendLoadfile(file, i, append, request.Title);
            }
        }

        if (string.IsNullOrEmpty(GetPropertyString("path")))
        {
            Log.Debug("mpv path property is empty after LoadFiles; setting playlist-pos to 0.");
            SetPropertyInt("playlist-pos", 0);
        }
    }

    void BeginNewMediaLoad()
    {
        lock (_mediaLoadStateLock)
        {
            _mediaLoadGeneration++;
            _liveReconnectAttempts = 0;
            _scheduledLiveReconnectGeneration = 0;
        }
    }

    public void CancelLiveStreamRecovery()
    {
        lock (_mediaLoadStateLock)
        {
            _mediaLoadGeneration++;
            _liveReconnectAttempts = 0;
            _scheduledLiveReconnectGeneration = 0;
        }
    }

    void LoadPlaylistItems(List<PlaylistFileItem> items, bool append)
    {
        Log.Debug($"Queueing playlist items for individual loadfile commands. itemCount={items.Count}, mode={(append ? "append" : "replace")}");
        SchedulePlayerTask(cancellationToken =>
        {
            for (int index = 0; index < items.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlaylistFileItem item = items[index];
                MediaLoadRequest? request = MediaInputNormalizer.Normalize(
                    item.Path, MediaInputSource.Playlist, append || index > 0, item.Title);
                if (request is not null)
                    SendLoadfile(request.Input, index, request.Append, request.Title);
            }
        });
    }

    bool TryLoadFallbackDirect(string? fallbackInput, string failedInput, bool append, string reason)
    {
        if (string.IsNullOrWhiteSpace(fallbackInput))
            return false;

        if (GetPlaylistPathKey(fallbackInput) == GetPlaylistPathKey(failedInput))
            return false;

        Log.Debug($"Playback fallback activated. reason='{reason}', fallback='{Log.SafeValue(fallbackInput)}', failedInput='{Log.SafeValue(failedInput)}', append={append}");
        SendLoadfile(ConvertFilePath(fallbackInput), append ? 1 : 0, append);

        return true;
    }

    void SendLoadfile(string file, int index, bool append, string? title = null)
    {
        MediaInputClassification classification = MediaInputClassifier.Classify(file);
        IReadOnlySet<string> explicitOptions = classification.IsNetwork
            ? MpvOptionConfiguration.GetExplicitOptions()
            : MpvOptionConfiguration.EmptyOptions;
        NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(classification, explicitOptions);

        if (resolution.IsEnabled)
            Log.Debug($"Applying network cache policy. kind={resolution.Kind}, profile={resolution.Profile}, path='{Log.SafeValue(file)}', options='{resolution.Options}'");

        if (index == 0 && !append)
            Log.Debug($"Sending loadfile replace to mpv: '{Log.SafeValue(file)}'");
        else
            Log.Debug($"Sending loadfile append to mpv: '{Log.SafeValue(file)}'");

        CommandV(BuildLoadfileArgs(file, index, append, title, resolution, explicitOptions));
    }

    public static bool ShouldUseAutomaticStreamingOptions(string file) =>
        NetworkCachePolicy.Resolve(file).IsEnabled;

    public static string[] BuildLoadfileArgs(string file, int index, bool append)
        => BuildLoadfileArgs(file, index, append, null);

    public static string[] BuildLoadfileArgs(string file, int index, bool append, string? title)
    {
        MediaInputClassification classification = MediaInputClassifier.Classify(file);
        IReadOnlySet<string> explicitOptions = classification.IsNetwork
            ? MpvOptionConfiguration.GetExplicitOptions()
            : MpvOptionConfiguration.EmptyOptions;
        NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(classification, explicitOptions);
        return BuildLoadfileArgs(file, index, append, title, resolution, explicitOptions);
    }

    static string[] BuildLoadfileArgs(
        string file,
        int index,
        bool append,
        string? title,
        NetworkCacheResolution resolution,
        IReadOnlySet<string> explicitOptions)
    {
        string mode = index == 0 && !append ? "replace" : "append";
        string options = resolution.Options;

        if (!string.IsNullOrWhiteSpace(title))
            options = string.IsNullOrEmpty(options)
                ? "force-media-title=" + EscapeLoadfileOption(title)
                : options + ",force-media-title=" + EscapeLoadfileOption(title);

        if (YouTubeMediaPolicy.ShouldEnableNativePlaylist(file) &&
            !MpvOptionConfiguration.IsExplicit(explicitOptions, "ytdl-raw-options") &&
            !MpvOptionConfiguration.IsExplicit(explicitOptions, "ytdl-raw-options-append"))
        {
            options = string.IsNullOrEmpty(options)
                ? YouTubeMediaPolicy.NativePlaylistLoadOption
                : options + "," + YouTubeMediaPolicy.NativePlaylistLoadOption;
        }

        if (!string.IsNullOrEmpty(options))
            return ["loadfile", file, mode, LoadfileOptionsInsertionIndex, options];

        return index == 0 && !append
            ? ["loadfile", file]
            : ["loadfile", file, mode];
    }

    static string EscapeLoadfileOption(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal);

    bool PlaylistContainsPath(string path)
    {
        string json = GetPropertyString("playlist");

        if (string.IsNullOrWhiteSpace(json))
            return false;

        string key = GetPlaylistPathKey(path);

        try
        {
            foreach (JsonElement item in JsonDocument.Parse(json).RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("filename", out JsonElement filenameElement))
                    continue;

                string? filename = filenameElement.GetString();

                if (!string.IsNullOrWhiteSpace(filename) && GetPlaylistPathKey(filename) == key)
                    return true;
            }
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("Playlist duplicate detection", path, ex);
        }

        return false;
    }

    static string GetPlaylistPathKey(string path)
    {
        if (FileTypes.IsStreamingUrl(path))
            return path.Trim();

        try
        {
            return System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).ToLowerInvariant();
        }
        catch
        {
            return path.Trim().ToLowerInvariant();
        }
    }

    public static string ConvertFilePath(string path)
    {
        if ((path.Contains(":/") && !path.Contains("://")) || (path.Contains(":\\") && path.Contains('/')))
            path = path.Replace("/", "\\");

        if (!path.Contains(':') && !path.StartsWith("\\\\") && File.Exists(path))
            path = System.IO.Path.GetFullPath(path);

        if (OperatingSystem.IsWindows() &&
            path.Length >= 260 &&
            !path.StartsWith(@"\\?\") &&
            !path.Contains("://") &&
            System.IO.Path.IsPathFullyQualified(path) &&
            File.Exists(path))
        {
            if (path.StartsWith(@"\\"))
                return @"\\?\UNC\" + path[2..];

            return @"\\?\" + path;
        }

        return path;
    }

    public void LoadISO(string path)
        => SchedulePlayerTask(cancellationToken => LoadISOAsync(path, cancellationToken));

    async Task LoadISOAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var mi = new MediaInfo(path);

            if (mi.GetGeneral("Format") == "ISO 9660 / DVD Video")
            {
                Command("stop");
                await Task.Delay(500, cancellationToken);
                SetPropertyString("dvd-device", path);
                LoadFiles([@"dvd://"], false, false, source: MediaInputSource.InternalCommand);
            }
            else
            {
                Command("stop");
                await Task.Delay(500, cancellationToken);
                SetPropertyString("bluray-device", path);
                LoadFiles([@"bd://"], false, false, source: MediaInputSource.InternalCommand);
            }
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("MediaInfo ISO detection", path, ex);
            CommandV("loadfile", path);
        }
    }

    public void LoadDiskFolder(string path)
        => SchedulePlayerTask(cancellationToken => LoadDiskFolderAsync(path, cancellationToken));

    async Task LoadDiskFolderAsync(string path, CancellationToken cancellationToken)
    {
        Command("stop");
        await Task.Delay(500, cancellationToken);

        if (Directory.Exists(path + "\\BDMV"))
        {
            SetPropertyString("bluray-device", path);
            LoadFiles([@"bd://"], false, false, source: MediaInputSource.InternalCommand);
        }
        else
        {
            SetPropertyString("dvd-device", path);
            LoadFiles([@"dvd://"], false, false, source: MediaInputSource.InternalCommand);
        }
    }

    async Task LoadFolderAsync(CancellationToken cancellationToken)
    {
        if (!App.AutoLoadFolder)
            return;

        try
        {
            await Task.Delay(1000, cancellationToken);

            lock (_loadFolderLock)
            {
                string path = GetPropertyString("path");
                Log.Debug($"Auto-load folder check. currentPath='{Log.SafeValue(path)}'");

                if (_isNormalizingAutocreatedPlaylist || !File.Exists(path))
                    return;

                int playlistCount = GetPropertyInt("playlist-count");

                if (playlistCount != 1)
                {
                    NormalizeAutocreatedPlaylist();
                    return;
                }

                string dir = Environment.CurrentDirectory;

                if (path.Contains(":/") && !path.Contains("://"))
                    path = path.Replace("/", "\\");

                if (path.Contains('\\'))
                    dir = System.IO.Path.GetDirectoryName(path)!;

                List<string> files = FileTypes.GetFolderMediaFiles(Directory.GetFiles(dir), path).ToList();
                Log.Debug($"Auto-load folder found candidate files. directory='{Log.SafeValue(dir)}', count={files.Count}");

                if (OperatingSystem.IsWindows())
                    files.Sort(new StringLogicalComparer());

                List<PlaylistFileItem> playlistItems = BuildFolderPlaylistItems(files);
                int index = playlistItems.FindIndex(i => GetPlaylistPathKey(i.Path) == GetPlaylistPathKey(path));

                if (playlistItems.Count == 0)
                    return;

                playlistItems.RemoveAll(i => GetPlaylistPathKey(i.Path) == GetPlaylistPathKey(path));

                if (playlistItems.Count > 0)
                    LoadPlaylistItems(playlistItems, true);

                if (index > 0)
                    CommandV("playlist-move", "0", (index + 1).ToString());
            }
        }
        finally
        {
            FinishAutoLoadFolder();
        }
    }

    static List<PlaylistFileItem> BuildFolderPlaylistItems(IEnumerable<string> files)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<PlaylistFileItem> ret = [];

        foreach (string file in files)
        {
            IEnumerable<PlaylistFileItem> items;

            try
            {
                items = FileTypes.IsPlaylist(file.Ext())
                    ? PlaylistFile.Read(file)
                    : [new PlaylistFileItem(file, "")];
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Folder playlist expansion skipped because a playlist file failed. path='{Log.SafeValue(file)}'");
                continue;
            }

            foreach (var item in items)
            {
                string key = GetPlaylistPathKey(item.Path);

                if (!seen.Add(key))
                    continue;

                ret.Add(item);
            }
        }

        return ret;
    }

    void ScheduleAutocreatedPlaylistNormalization() =>
        SchedulePlayerTask(async cancellationToken =>
        {
            await Task.Delay(PlaylistNormalizationDelay, cancellationToken);
            NormalizeAutocreatedPlaylist();
        });

    void NormalizeAutocreatedPlaylist()
    {
        if (_isNormalizingAutocreatedPlaylist)
            return;

        int playlistCount = GetPropertyInt("playlist-count");
        if (!ShouldNormalizeAutocreatedPlaylist(playlistCount, playbackActive: true))
            return;

        List<PlaylistFileItem> items = [];

        for (int index = 0; index < playlistCount; index++)
        {
            string title = GetPropertyString($"playlist/{index}/title");
            string filename = GetPropertyString($"playlist/{index}/filename");
            items.Add(new PlaylistFileItem(ConvertFilePath(filename), title));
        }

        List<PlaylistFileItem> normalizedItems = PlaylistFile.NormalizeExisting(items);
        bool hasDuplicate = normalizedItems.Count != items.Count;
        bool hasTitleChanges = hasDuplicate ||
            items.Select((item, index) => !string.Equals(item.Title, normalizedItems[index].Title, StringComparison.Ordinal))
                .Any(changed => changed);

        if ((!hasDuplicate && !hasTitleChanges) || items.Count == 0)
            return;

        try
        {
            _isNormalizingAutocreatedPlaylist = true;
            RemoveDuplicatePlaylistEntries(items);
            NormalizePlaylistEntryTitles();
        }
        finally
        {
            _isNormalizingAutocreatedPlaylist = false;
        }
    }

    void RemoveDuplicatePlaylistEntries(IReadOnlyList<PlaylistFileItem> items)
    {
        Dictionary<string, int> retainedIndexes = new(StringComparer.OrdinalIgnoreCase);
        List<int> duplicateIndexes = [];
        int playingPosition = GetPropertyInt("playlist-playing-pos");

        if (playingPosition < 0)
            playingPosition = GetPropertyInt("playlist-pos");

        for (int index = 0; index < items.Count; index++)
        {
            string key = GetPlaylistPathKey(items[index].Path);

            if (!retainedIndexes.TryGetValue(key, out int retainedIndex))
            {
                retainedIndexes[key] = index;
                continue;
            }

            if (index == playingPosition && retainedIndex != playingPosition)
            {
                duplicateIndexes.Add(retainedIndex);
                retainedIndexes[key] = index;
            }
            else
            {
                duplicateIndexes.Add(index);
            }
        }

        for (int index = duplicateIndexes.Count - 1; index >= 0; index--)
        {
            int playlistIndex = duplicateIndexes[index];
            Log.Debug($"Removing duplicate playlist entry. index={playlistIndex}, path='{Log.SafeValue(items[playlistIndex].Path)}'");
            CommandV("playlist-remove", playlistIndex.ToString());
        }
    }

    void NormalizePlaylistEntryTitles()
    {
        int playlistCount = GetPropertyInt("playlist-count");
        int playingPosition = GetPropertyInt("playlist-playing-pos");

        if (playingPosition < 0)
            playingPosition = GetPropertyInt("playlist-pos");

        for (int index = playlistCount - 1; index >= 0; index--)
        {
            string filename = ConvertFilePath(GetPropertyString($"playlist/{index}/filename"));
            string title = GetPropertyString($"playlist/{index}/title");
            string normalizedTitle = PlaylistFile.NormalizeDisplayTitles([new PlaylistFileItem(filename, title)])[0].Title;

            if (string.Equals(title, normalizedTitle, StringComparison.Ordinal))
                continue;

            if (index == playingPosition)
            {
                SetPropertyString("file-local-options/force-media-title", normalizedTitle);
                continue;
            }

            CommandV("playlist-remove", index.ToString());
            CommandV(BuildPlaylistInsertArgs(filename, index, normalizedTitle));
            Log.Debug($"Normalized playlist entry title. index={index}, title='{Log.SafeValue(normalizedTitle)}'");
        }
    }

    internal static string[] BuildPlaylistInsertArgs(string file, int playlistIndex, string title)
    {
        MediaInputClassification classification = MediaInputClassifier.Classify(file);
        IReadOnlySet<string> explicitOptions = classification.IsNetwork
            ? MpvOptionConfiguration.GetExplicitOptions()
            : MpvOptionConfiguration.EmptyOptions;
        NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(classification, explicitOptions);
        string options = resolution.Options;

        if (!string.IsNullOrWhiteSpace(title))
            options = string.IsNullOrEmpty(options)
                ? "force-media-title=" + EscapeLoadfileOption(title)
                : options + ",force-media-title=" + EscapeLoadfileOption(title);

        return string.IsNullOrEmpty(options)
            ? ["loadfile", file, "insert-at", playlistIndex.ToString()]
            : ["loadfile", file, "insert-at", playlistIndex.ToString(), options];
    }

    [SupportedOSPlatform("windows")]
    void LoadAviSynth()
    {
        if (!_wasAviSynthLoaded)
        {
            string? dll = Environment.GetEnvironmentVariable("AviSynthDLL");  // StaxRip sets it in portable mode
            LoadLibrary(File.Exists(dll) ? dll : "AviSynth.dll");
            _wasAviSynthLoaded = true;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadLibrary(string path);

    [SupportedOSPlatform("windows")]
    public static string GetShortcutTarget(string path)
    {
        Type? t = Type.GetTypeFromProgID("WScript.Shell");
        dynamic? sh = Activator.CreateInstance(t!);
        return sh?.CreateShortcut(path).TargetPath!;
    }
}
