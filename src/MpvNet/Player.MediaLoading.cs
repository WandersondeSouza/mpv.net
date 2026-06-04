using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
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
    public void SetBluRayTitle(int id) => LoadFiles(new[] { @"bd://" + id }, false, false);

    public DateTime LastLoad;

    public void LoadFiles(string[]? files, bool loadFolder, bool append)
    {
        if (files == null || files.Length == 0)
            return;

        if ((DateTime.Now - LastLoad).TotalMilliseconds < 1000)
            append = true;

        LastLoad = DateTime.Now;

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];

            if (string.IsNullOrEmpty(file))
                continue;

            if (file.Contains('|'))
                file = file[..file.IndexOf("|")];

            file = ConvertFilePath(file);

            if (TryDownloadRemotePlaylist(file, out string remotePlaylistFile))
                file = remotePlaylistFile;

            string ext = file.Ext();

            if (OperatingSystem.IsWindows())
            {
                switch (ext)
                {
                    case "avs": LoadAviSynth(); break;
                    case "lnk": file = GetShortcutTarget(file); break;
                }
            }

            if (FileTypes.IsPlaylist(ext) && File.Exists(file))
            {
                var playlistItems = PlaylistFile.Read(file);
                bool appendPlaylist = append || i > 0 || !string.IsNullOrEmpty(GetPropertyString("path"));
                List<PlaylistFileItem> itemsToLoad = [];

                foreach (var item in playlistItems)
                {
                    if (PlaylistContainsPath(item.Path))
                        continue;

                    itemsToLoad.Add(item);
                }

                if (itemsToLoad.Count > 0)
                    LoadPlaylistItems(itemsToLoad, appendPlaylist);
            }
            else if (ext == "iso")
                LoadISO(file);
            else if(FileTypes.Subtitle.Contains(ext))
                CommandV("sub-add", file);
            else
            {
                if (i == 0 && !append)
                    CommandV("loadfile", file);
                else
                    CommandV("loadfile", file, "append");
            }
        }

        if (string.IsNullOrEmpty(GetPropertyString("path")))
            SetPropertyInt("playlist-pos", 0);
    }

    static bool TryDownloadRemotePlaylist(string file, out string playlistFile)
    {
        playlistFile = "";

        if (!ShouldProbeRemotePlaylist(file))
            return false;

        try
        {
            using HttpRequestMessage probeRequest = new(HttpMethod.Get, file);
            using HttpResponseMessage probeResponse = RemotePlaylistHttpClient.Send(
                probeRequest, HttpCompletionOption.ResponseHeadersRead);

            if (!probeResponse.IsSuccessStatusCode)
                return false;

            using Stream probeStream = probeResponse.Content.ReadAsStream();
            byte[] buffer = new byte[4096];
            int read = probeStream.Read(buffer, 0, buffer.Length);

            if (!LooksLikeM3u(buffer.AsSpan(0, read)))
                return false;

            string content = RemotePlaylistHttpClient.GetStringAsync(file).GetAwaiter().GetResult();
            Directory.CreateDirectory(App.TempFolder);
            string tempFile = System.IO.Path.Combine(App.TempFolder, Guid.NewGuid() + ".m3u8");
            File.WriteAllText(tempFile, content, Encoding.UTF8);
            App.TempFiles.Add(tempFile);
            playlistFile = tempFile;
            return true;
        }
        catch (Exception ex)
        {
            LogRemotePlaylistDetectionFailure(file, ex);
            return false;
        }
    }

    static void LogRemotePlaylistDetectionFailure(string file, Exception ex)
    {
        if (IsRemotePlaylistProbeTimeout(ex))
        {
            Log.Debug($"Remote playlist detection timed out for '{file}': {ex.Message}");
            return;
        }

        LogNonBlockingMetadataFailure("Remote playlist detection", file, ex);
    }

    public static bool IsRemotePlaylistProbeTimeout(Exception ex) =>
        ex is TaskCanceledException or TimeoutException ||
        ex.InnerException != null && IsRemotePlaylistProbeTimeout(ex.InnerException);

    static bool ShouldProbeRemotePlaylist(string file)
    {
        if (!Uri.TryCreate(file, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (FileTypes.IsPlaylistFile(file) || FileTypes.IsVideoFile(file))
            return false;

        return true;
    }

    public static bool LooksLikeM3u(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return false;

        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];

        if (bytes.StartsWith(utf8Bom))
            bytes = bytes[utf8Bom.Length..];

        while (!bytes.IsEmpty && (bytes[0] == ' ' || bytes[0] == '\t' || bytes[0] == '\r' || bytes[0] == '\n'))
            bytes = bytes[1..];

        return bytes.StartsWith("#EXTM3U"u8);
    }

    void LoadPlaylistItems(List<PlaylistFileItem> items, bool append)
    {
        string playlist = PlaylistFile.WriteTempM3u(items);
        CommandV("loadlist", playlist, append ? "append" : "replace");
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

            List<string> files = FileTypes.GetMediaFiles(Directory.GetFiles(dir)).ToList();

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
            IEnumerable<PlaylistFileItem> items = FileTypes.IsPlaylist(file.Ext())
                ? PlaylistFile.Read(file)
                : [new PlaylistFileItem(file, "")];

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
