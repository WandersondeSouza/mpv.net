using System.Net.Http;
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
    public const string AutomaticStreamingLoadOptions =
        "cache=yes,cache-pause-initial=yes,cache-pause-wait=60,demuxer-max-bytes=128MiB,network-timeout=60";

    public void SetBluRayTitle(int id) => LoadFiles(new[] { @"bd://" + id }, false, false);

    public DateTime LastLoad;

    public void LoadFiles(string[]? files, bool loadFolder, bool append, string? fallbackInput = null)
    {
        if (files == null || files.Length == 0)
        {
            Log.Info($"LoadFiles skipped because no files were supplied. loadFolder={loadFolder}, append={append}");
            return;
        }

        if ((DateTime.Now - LastLoad).TotalMilliseconds < 1000)
        {
            Log.Debug("LoadFiles called within 1000 ms of previous load; forcing append mode.");
            append = true;
        }

        LastLoad = DateTime.Now;
        Log.Info($"Loading media inputs. count={files.Length}, loadFolder={loadFolder}, append={append}, fallback='{Log.SafeValue(fallbackInput)}', inputs={Log.SafeValues(files)}");

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];

            if (string.IsNullOrEmpty(file))
            {
                Log.Debug($"Skipping empty media input at index {i}.");
                continue;
            }

            if (file.Contains('|'))
            {
                Log.Debug($"Removing display-title suffix from media input at index {i}: '{Log.SafeValue(file)}'");
                file = file[..file.IndexOf("|")];
            }

            string originalFile = file;
            file = ConvertFilePath(file);
            Log.Debug($"Prepared media input at index {i}: original='{Log.SafeValue(originalFile)}', converted='{Log.SafeValue(file)}'");

            if (TryDownloadRemotePlaylist(file, out string remotePlaylistFile))
            {
                Log.Info($"Remote playlist detected and downloaded. source='{Log.SafeValue(file)}', tempFile='{Log.SafeValue(remotePlaylistFile)}'");
                file = remotePlaylistFile;
            }

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
                    Log.Info($"Playlist file expanded. path='{Log.SafeValue(file)}', parsedItems={playlistItems.Count}, appendPlaylist={appendPlaylist}");

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
                        Log.Info($"Loading playlist items. playlist='{Log.SafeValue(file)}', count={itemsToLoad.Count}, append={appendPlaylist}");
                        LoadPlaylistItems(itemsToLoad, appendPlaylist);
                    }
                    else
                    {
                        Log.Info($"Playlist file did not add new items. path='{Log.SafeValue(file)}'");

                        if (string.IsNullOrEmpty(GetPropertyString("path")))
                            TryLoadFallbackDirect(fallbackInput, file, appendPlaylist, "empty playlist expansion");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Playlist expansion failed; playback fallback will be attempted. playlist='{Log.SafeValue(file)}', fallback='{Log.SafeValue(fallbackInput)}'");

                    if (!TryLoadFallbackDirect(fallbackInput, file, appendPlaylist, "playlist expansion failure"))
                    {
                        Log.Info($"Falling back to raw playlist file through mpv. playlist='{Log.SafeValue(file)}'");
                        SendLoadfile(file, i, append);
                    }
                }
            }
            else if (ext == "iso")
            {
                Log.Info($"Loading ISO media input: '{Log.SafeValue(file)}'");
                LoadISO(file);
            }
            else if(FileTypes.Subtitle.Contains(ext))
            {
                Log.Info($"Adding subtitle from media input: '{Log.SafeValue(file)}'");
                CommandV("sub-add", file);
            }
            else
            {
                SendLoadfile(file, i, append);
            }
        }

        if (string.IsNullOrEmpty(GetPropertyString("path")))
        {
            Log.Debug("mpv path property is empty after LoadFiles; setting playlist-pos to 0.");
            SetPropertyInt("playlist-pos", 0);
        }
    }

    static bool TryDownloadRemotePlaylist(string file, out string playlistFile)
        => RemotePlaylistService.TryDownload(
            file, RemotePlaylistHttpClient, App.TempFolder, App.TempFiles, out playlistFile);

    public static bool IsRemotePlaylistProbeTimeout(Exception ex) =>
        RemotePlaylistService.IsProbeTimeout(ex);

    public static bool LooksLikeM3u(ReadOnlySpan<byte> bytes) =>
        RemotePlaylistService.LooksLikeM3u(bytes);

    void LoadPlaylistItems(List<PlaylistFileItem> items, bool append)
    {
        string playlist = PlaylistFile.WriteTempM3u(items);
        Log.Info($"Sending loadlist to mpv. tempPlaylist='{Log.SafeValue(playlist)}', itemCount={items.Count}, mode={(append ? "append" : "replace")}");
        CommandV("loadlist", playlist, append ? "append" : "replace");
    }

    bool TryLoadFallbackDirect(string? fallbackInput, string failedInput, bool append, string reason)
    {
        if (string.IsNullOrWhiteSpace(fallbackInput))
            return false;

        if (GetPlaylistPathKey(fallbackInput) == GetPlaylistPathKey(failedInput))
            return false;

        Log.Info($"Playback fallback activated. reason='{reason}', fallback='{Log.SafeValue(fallbackInput)}', failedInput='{Log.SafeValue(failedInput)}', append={append}");
        SendLoadfile(ConvertFilePath(fallbackInput), append ? 1 : 0, append);

        return true;
    }

    void SendLoadfile(string file, int index, bool append)
    {
        bool useStreamingOptions = ShouldUseAutomaticStreamingOptions(file);

        if (useStreamingOptions)
            Log.Info($"Applying automatic streaming network tolerance to loadfile. path='{Log.SafeValue(file)}', options='{AutomaticStreamingLoadOptions}'");

        if (index == 0 && !append)
            Log.Info($"Sending loadfile replace to mpv: '{Log.SafeValue(file)}'");
        else
            Log.Info($"Sending loadfile append to mpv: '{Log.SafeValue(file)}'");

        CommandV(BuildLoadfileArgs(file, index, append));
    }

    public static bool ShouldUseAutomaticStreamingOptions(string file) =>
        FileTypes.IsStreamingUrl(file);

    public static string[] BuildLoadfileArgs(string file, int index, bool append)
    {
        string mode = index == 0 && !append ? "replace" : "append";

        if (ShouldUseAutomaticStreamingOptions(file))
            return ["loadfile", file, mode, LoadfileOptionsInsertionIndex, AutomaticStreamingLoadOptions];

        return index == 0 && !append
            ? ["loadfile", file]
            : ["loadfile", file, mode];
    }

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
    {
        try
        {
            using var mi = new MediaInfo(path);

            if (mi.GetGeneral("Format") == "ISO 9660 / DVD Video")
            {
                Command("stop");
                Thread.Sleep(500);
                SetPropertyString("dvd-device", path);
                LoadFiles([@"dvd://"], false, false);
            }
            else
            {
                Command("stop");
                Thread.Sleep(500);
                SetPropertyString("bluray-device", path);
                LoadFiles([@"bd://"], false, false);
            }
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("MediaInfo ISO detection", path, ex);
            CommandV("loadfile", path);
        }
    }

    public void LoadDiskFolder(string path)
    {
        Command("stop");
        Thread.Sleep(500);

        if (Directory.Exists(path + "\\BDMV"))
        {
            SetPropertyString("bluray-device", path);
            LoadFiles([@"bd://"], false, false);
        }
        else
        {
            SetPropertyString("dvd-device", path);
            LoadFiles([@"dvd://"], false, false);
        }
    }

    public void LoadFolder()
    {
        if (!App.AutoLoadFolder)
            return;

        Thread.Sleep(1000);

        lock (LoadFolderLockObject)
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
        TaskHelp.Run(() => {
            Thread.Sleep(PlaylistNormalizationDelay);
            NormalizeAutocreatedPlaylist();
        });

    void NormalizeAutocreatedPlaylist()
    {
        if (_isNormalizingAutocreatedPlaylist)
            return;

        int playlistCount = GetPropertyInt("playlist-count");

        if (playlistCount <= 1)
            return;

        int playlistPos = GetPropertyInt("playlist-pos");
        List<PlaylistFileItem> items = [];
        bool needsNormalization = false;

        for (int index = 0; index < playlistCount; index++)
        {
            string title = GetPropertyString($"playlist/{index}/title");
            string filename = GetPropertyString($"playlist/{index}/filename");
            string path = ConvertFilePath(filename);
            items.Add(new PlaylistFileItem(path, title));
        }

        List<PlaylistFileItem> normalizedItems = PlaylistFile.NormalizeDisplayTitles(items);

        for (int index = 0; index < items.Count; index++)
            if (!string.Equals(items[index].Title, normalizedItems[index].Title, StringComparison.Ordinal))
                needsNormalization = true;

        if (!needsNormalization || items.Count == 0)
            return;

        try
        {
            _isNormalizingAutocreatedPlaylist = true;
            LoadPlaylistItems(normalizedItems, false);

            if (playlistPos >= 0 && playlistPos < normalizedItems.Count)
                SetPropertyInt("playlist-pos", playlistPos);
        }
        finally
        {
            _isNormalizingAutocreatedPlaylist = false;
        }
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
