
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using MpvNet.Extensions;
using MpvNet.Help;
using MpvNet.Native;

using static MpvNet.Native.LibMpv;

namespace MpvNet;

public partial class MainPlayer : MpvClient
{
    static readonly TimeSpan PlaylistNormalizationDelay = TimeSpan.FromMilliseconds(200);
    bool _isNormalizingAutocreatedPlaylist;
    readonly object _destroyLock = new();
    bool _isDestroyed;
    readonly CancellationTokenSource _playerCancellation = new();
    readonly SemaphoreSlim _playerTaskGate = new(1, 1);
    readonly object _playerTasksLock = new();
    readonly List<Task> _playerTasks = [];
    readonly object _eventTasksLock = new();
    readonly List<Task> _eventTasks = [];
    bool _mpvInitialized;

    public PlayerLifecycleState LifecycleState { get; private set; } = PlayerLifecycleState.Created;
    internal CancellationToken PlayerCancellationToken => _playerCancellation.Token;

    public void SchedulePlayerTask(Action<CancellationToken> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        SchedulePlayerTask(cancellationToken =>
        {
            operation(cancellationToken);
            return Task.CompletedTask;
        });
    }

    public void SchedulePlayerTask(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_destroyLock)
        {
            if (_isDestroyed)
                return;

            Task task = Task.Run(() => RunPlayerTaskAsync(operation), _playerCancellation.Token);
            lock (_playerTasksLock)
                _playerTasks.Add(task);
            task.ContinueWith(completedTask =>
            {
                lock (_playerTasksLock)
                    _playerTasks.Remove(completedTask);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    async Task RunPlayerTaskAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await _playerTaskGate.WaitAsync(_playerCancellation.Token).ConfigureAwait(false);
            try
            {
                _playerCancellation.Token.ThrowIfCancellationRequested();
                await operation(_playerCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                _playerTaskGate.Release();
            }
        }
        catch (OperationCanceledException) when (_playerCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Terminal.WriteError(ex);
        }
    }

    internal void TrackEventTask(Task task)
    {
        lock (_eventTasksLock)
            _eventTasks.Add(task);
        task.ContinueWith(completedTask =>
        {
            lock (_eventTasksLock)
                _eventTasks.Remove(completedTask);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    internal void SetMpvInitialized() => _mpvInitialized = true;

    public string ConfPath { get => ConfigFolder + "mpv.conf"; }
    public string CacheFolder => TemporaryFileCleanup.DefaultCacheFolder + System.IO.Path.DirectorySeparatorChar;
    public string GPUAPI { get; set; } = "auto";
    public string Path { get; set; } = "";
    public string VO { get; set; } = "gpu";
    public string UsedInputConfContent { get; set; } = "";

    public string VID { get; set; } = "";
    public string AID { get; set; } = "";
    public string SID { get; set; } = "";

    public bool Border { get; set; } = true;
    public bool FileEnded { get; set; }
    public bool Fullscreen { get; set; }
    public bool IsQuitNeeded { set; get; } = true;
    public bool KeepaspectWindow { get; set; }
    public bool Paused { get; set; }
    public bool SnapWindow { get; set; }
    public bool TaskbarProgress { get; set; } = true;
    public bool TitleBar { get; set; } = true;
    public bool WasInitialSizeSet;
    public bool WindowMaximized { get; set; }
    public bool WindowMinimized { get; set; }

    public int Edition { get; set; }
    public int PlaylistPos { get; set; } = -1;
    public int Screen { get; set; } = -1;
    public int VideoRotate { get; set; }

    public float Autofit { get; set; } = 0.6f;
    public float AutofitSmaller { get; set; } = 0.3f;
    public float AutofitLarger { get; set; } = 0.8f;

    public AutoResetEvent ShutdownAutoResetEvent { get; } = new AutoResetEvent(false);
    public nint MainHandle { get; set; }
    public List<MediaTrack> MediaTracks { get; set; } = new List<MediaTrack>();
    public List<TimeSpan> BluRayTitles { get; } = new List<TimeSpan>();
    public object MediaTracksLock { get; } = new object();
    public Size VideoSize { get; set; }
    public TimeSpan Duration;
    public List<MpvClient> Clients { get; } = new List<MpvClient>();

    List<StringPair>? _audioDevices;

    public event Action? Initialized;
    public event Action? Pause;
    public event Action<int>? PlaylistPosChanged;
    public event Action<Size>? VideoSizeChanged;

    public void ProcessProperty(string? name, string? value)
    {
        switch (name)
        {
            case "autofit":
                {
                    if (int.TryParse(value?.Trim('%'), out int result))
                        Autofit = result / 100f;
                }
                break;
            case "autofit-smaller":
                {
                    if (int.TryParse(value?.Trim('%'), out int result))
                        AutofitSmaller = result / 100f;
                }
                break;
            case "autofit-larger":
                {
                    if (int.TryParse(value?.Trim('%'), out int result))
                        AutofitLarger = result / 100f;
                }
                break;
            case "border": Border = value == "yes"; break;
            case "fs":
            case "fullscreen": Fullscreen = value == "yes"; break;
            case "gpu-api" when value is not null: GPUAPI = value; break;
            case "keepaspect-window": KeepaspectWindow = value == "yes"; break;
            case "screen": Screen = Convert.ToInt32(value); break;
            case "snap-window": SnapWindow = value == "yes"; break;
            case "taskbar-progress": TaskbarProgress = value == "yes"; break;
            case "vo" when value is not null: VO = value; break;
            case "window-maximized": WindowMaximized = value == "yes"; break;
            case "window-minimized": WindowMinimized = value == "yes"; break;
            case "title-bar": TitleBar = value == "yes"; break;
        }

        if (AutofitLarger > 1)
            AutofitLarger = 1;
    }

    string? _configFolder;

    public string ConfigFolder {
        get {
            if (_configFolder == null)
            {
                string? mpvnet_home = Environment.GetEnvironmentVariable("MPVNET_HOME");

                if (Directory.Exists(mpvnet_home))
                    return _configFolder = mpvnet_home.Separator();

                _configFolder = AppPaths.PortableConfig;

                if (!Directory.Exists(_configFolder))
                    _configFolder = AppPaths.DefaultConfig;

                if (!Directory.Exists(_configFolder))
                    Directory.CreateDirectory(_configFolder);

                _configFolder = AppPaths.WithTrailingSeparator(_configFolder);
            }

            return _configFolder;
        }
    }

    readonly Regex _configurationOptionNameRegex = new("^[\\w-]+$", RegexOptions.Compiled);

    Dictionary<string, string>? _Conf;

    public Dictionary<string, string> Conf {
        get
        {
            if (_Conf != null)
                return _Conf;

            App.ApplyInputDefaultBindingsFix();

            _Conf = [];

            if (File.Exists(ConfPath))
            {
                foreach (string? it in File.ReadAllLines(ConfPath))
                {
                    string line = it.TrimStart(' ', '-').TrimEnd();

                    if (line.StartsWith('#'))
                        continue;

                    if (!line.Contains('='))
                    {
                        if (_configurationOptionNameRegex.IsMatch(line))
                            line += "=yes";
                        else
                            continue;
                    }

                    string key = line[..line.IndexOf("=")].Trim();
                    string value = line[(line.IndexOf("=") + 1)..].Trim();

                    if (value.Contains('#') && !value.StartsWith("#") &&
                        !value.StartsWith("'#") && !value.StartsWith("\"#"))

                        value = value[..value.IndexOf("#")].Trim();

                    _Conf[key] = value;
                }
            }

            foreach (var i in _Conf)
            {
                ProcessProperty(i.Key, i.Value);
            }

            return _Conf;
        }
    }

    void UpdateVideoSize(string w, string h)
    {
        if (string.IsNullOrEmpty(Path))
            return;

        Size size = new Size(GetPropertyInt(w), GetPropertyInt(h));

        if (VideoRotate == 90 || VideoRotate == 270)
            size = new Size(size.Height, size.Width);

        if (size != VideoSize && size != Size.Empty)
        {
            VideoSize = size;
            VideoSizeChanged?.Invoke(size);
        }
    }

    static readonly object _loadFolderLock = new();
    bool _wasAviSynthLoaded;

    static string GetLanguage(string id)
    {
        return LanguageNormalizer.GetDisplayName(id);
    }

    static string GetNativeLanguage(string name)
    {
        string? normalized = LanguageNormalizer.Normalize(name);
        if (normalized != null)
        {
            try
            {
                return CultureInfo.GetCultureInfo(normalized).NativeName;
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return name;
    }

    public void UpdateTracks()
    {
        string path = GetPropertyString("path");

        if (!path.ToLowerEx().StartsWithEx("bd://"))
            lock (BluRayTitles)
                BluRayTitles.Clear();

        lock (MediaTracksLock)
        {
            MediaTracks = GetSafeTracks(path);
        }
    }

    public List<MediaTrack> GetSafeTracks(string path)
    {
        if (CanUseMediaInfo(path))
        {
            try
            {
                return GetMediaInfoTracks(path);
            }
            catch (Exception ex)
            {
                LogNonBlockingMetadataFailure("MediaInfo track scan", path, ex);
            }
        }

        try
        {
            return GetTracks();
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("mpv track scan", path, ex);
            return [];
        }
    }

    public static bool CanUseMediaInfo(string path) =>
        MediaInfoPolicy.CanUseMediaInfo(App.MediaInfo, path);

    TimeSpan GetSafeDuration()
    {
        try
        {
            double seconds = GetPropertyDouble("duration", false);

            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                return TimeSpan.Zero;

            return TimeSpan.FromSeconds(seconds);
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("mpv duration property", Path, ex);
            return TimeSpan.Zero;
        }
    }

    static void LogNonBlockingMetadataFailure(string source, string path, Exception ex)
    {
        Log.Error(ex, $"Non-blocking metadata failure ({source}).");
        Terminal.WriteError($"Non-blocking metadata failure ({source}) for '{path}': {ex.Message}");
        Terminal.WriteError(ex);
    }

    public List<StringPair> AudioDevices {
        get {
            if (_audioDevices != null)
                return _audioDevices;

            _audioDevices = [];

            try
            {
                string json = GetPropertyString("audio-device-list");
                var enumerator = JsonDocument.Parse(json).RootElement.EnumerateArray();

                foreach (var element in enumerator)
                {
                    string name = element.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
                    string description = element.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() ?? "" : "";

                    if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(description))
                        _audioDevices.Add(new StringPair(name, description));
                }
            }
            catch (Exception ex)
            {
                LogNonBlockingMetadataFailure("mpv audio-device-list property", Path, ex);
            }

            return _audioDevices;
        }
    }

    public List<Chapter> GetChapters() {
        List<Chapter> chapters = new List<Chapter>();
        try
        {
            int count = GetPropertyInt("chapter-list/count");

            for (int x = 0; x < count; x++)
            {
                string title = GetPropertyString($"chapter-list/{x}/title");
                double time = GetPropertyDouble($"chapter-list/{x}/time", false);

                if (string.IsNullOrEmpty(title) ||
                    (title.Length == 12 && title.Contains(':') && title.Contains('.')))

                    title = "Chapter " + (x + 1);

                if (double.IsNaN(time) || double.IsInfinity(time) || time < 0)
                    time = 0;

                chapters.Add(new Chapter() { Title = title, Time = time });
            }
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("mpv chapter-list property", Path, ex);
        }

        return chapters;
    }

    public void UpdateExternalTracks()
    {
        try
        {
            int trackListTrackCount = GetPropertyInt("track-list/count");
            int editionCount = GetPropertyInt("edition-list/count");
            int count = MediaTracks.Where(i => i.Type != "g").Count();

            lock (MediaTracksLock)
            {
                if (count != (trackListTrackCount + editionCount))
                {
                    MediaTracks = MediaTracks.Where(i => !i.External).ToList();
                    MediaTracks.AddRange(GetSafeMpvTracks(false));
                }
            }
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("mpv external track scan", Path, ex);
        }
    }

    readonly Regex _leadingTitleSeparatorRegex = new(@"^[\._\-]", RegexOptions.Compiled);

    public List<MediaTrack> GetTracks(bool includeInternal = true, bool includeExternal = true)
    {
        List<MediaTrack> tracks = new List<MediaTrack>();

        int trackCount = GetPropertyInt("track-list/count");

        for (int i = 0; i < trackCount; i++)
        {
            bool external = GetPropertyBool($"track-list/{i}/external", false);

            if ((external && !includeExternal) || (!external && !includeInternal))
                continue;

            string type = GetPropertyString($"track-list/{i}/type");
            string filename = GetPropertyString($"filename/no-ext");
            string title = GetPropertyString($"track-list/{i}/title");

            if (!string.IsNullOrEmpty(filename))
                title = title.Replace(filename, "");

            title = _leadingTitleSeparatorRegex.Replace(title, "");

            if (type == "video")
            {
                string codec = GetPropertyString($"track-list/{i}/codec").ToUpperEx();
                if (codec == "MPEG2VIDEO")
                    codec = "MPEG2";
                else if (codec == "DVVIDEO")
                    codec = "DV";
                MediaTrack track = new MediaTrack();
                MediaTrackText.AddMpvValue(track, codec);
                MediaTrackText.AddMpvValue(track, GetPropertyString($"track-list/{i}/demux-w") + "x" + GetPropertyString($"track-list/{i}/demux-h"));
                MediaTrackText.AddMpvValue(track, GetPropertyString($"track-list/{i}/demux-fps").Replace(".000000", "") + " FPS");
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/default", false) ? _("Default") : null);
                track.Text = "V: " + track.Text.Trim(' ', ',');
                track.Type = "v";
                track.ID = GetPropertyInt($"track-list/{i}/id");
                tracks.Add(track);
            }
            else if (type == "audio")
            {
                string codec = GetPropertyString($"track-list/{i}/codec").ToUpperEx();
                string language = GetPropertyString($"track-list/{i}/lang");
                if (codec.Contains("PCM"))
                    codec = "PCM";
                MediaTrack track = new MediaTrack();
                track.Language = language;
                MediaTrackText.AddMpvValue(track, GetLanguage(language));
                MediaTrackText.AddMpvValue(track, codec);
                MediaTrackText.AddMpvValue(track, GetPropertyInt($"track-list/{i}/audio-channels") + " ch");
                MediaTrackText.AddMpvValue(track, GetPropertyInt($"track-list/{i}/demux-samplerate") / 1000 + " kHz");
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/forced", false) ? _("Forced") : null);
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/default", false) ? _("Default") : null);
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/external", false) ? _("External") : null);
                MediaTrackText.AddMpvValue(track, title);
                track.Text = "A: " + track.Text.Trim(' ', ',');
                track.Type = "a";
                track.ID = GetPropertyInt($"track-list/{i}/id");
                track.External = external;
                tracks.Add(track);
            }
            else if (type == "sub")
            {
                string codec = GetPropertyString($"track-list/{i}/codec").ToUpperEx();
                string language = GetPropertyString($"track-list/{i}/lang");
                if (codec.Contains("PGS"))
                    codec = "PGS";
                else if (codec == "SUBRIP")
                    codec = "SRT";
                else if (codec == "WEBVTT")
                    codec = "VTT";
                else if (codec == "DVB_SUBTITLE")
                    codec = "DVB";
                else if (codec == "DVD_SUBTITLE")
                    codec = "VOB";
                MediaTrack track = new MediaTrack();
                track.Language = language;
                MediaTrackText.AddMpvValue(track, GetLanguage(language));
                MediaTrackText.AddMpvValue(track, codec);
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/forced", false) ? _("Forced") : null);
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/default", false) ? _("Default") : null);
                MediaTrackText.AddMpvValue(track, GetPropertyBool($"track-list/{i}/external", false) ? _("External") : null);
                MediaTrackText.AddMpvValue(track, title);
                track.Text = "S: " + track.Text.Trim(' ', ',');
                track.Type = "s";
                track.ID = GetPropertyInt($"track-list/{i}/id");
                track.External = external;
                tracks.Add(track);
            }
        }

        if (includeInternal)
        {
            int editionCount = GetPropertyInt("edition-list/count");

            for (int i = 0; i < editionCount; i++)
            {
                string title = GetPropertyString($"edition-list/{i}/title");

                if (string.IsNullOrEmpty(title))
                    title = _("Edition") + " " + i;

                MediaTrack track = new MediaTrack
                {
                    Text = "E: " + title,
                    Type = "e",
                    ID = i
                };

                tracks.Add(track);
            }
        }

        return tracks;

    }

    List<MediaTrack> GetSafeMpvTracks(bool includeInternal = true, bool includeExternal = true)
    {
        try
        {
            return GetTracks(includeInternal, includeExternal);
        }
        catch (Exception ex)
        {
            LogNonBlockingMetadataFailure("mpv track-list property", Path, ex);
            return [];
        }
    }

    public List<MediaTrack> GetMediaInfoTracks(string path)
    {
        List<MediaTrack> tracks = new List<MediaTrack>();

        using (MediaInfo mi = new MediaInfo(path))
        {
            MediaTrack track = new MediaTrack();
            MediaTrackText.AddMediaInfoValue(track, mi.GetGeneral("Format"));
            MediaTrackText.AddMediaInfoValue(track, mi.GetGeneral("FileSize/String"));
            MediaTrackText.AddMediaInfoValue(track, mi.GetGeneral("Duration/String"));
            MediaTrackText.AddMediaInfoValue(track, mi.GetGeneral("OverallBitRate/String"));
            track.Text = "G: " + track.Text.Trim(' ', ',');
            track.Type = "g";
            tracks.Add(track);

            int videoCount = mi.GetCount(MediaInfoStreamKind.Video);

            for (int i = 0; i < videoCount; i++)
            {
                string fps = mi.GetVideo(i, "FrameRate");

                if (float.TryParse(fps, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                    fps = result.ToString(CultureInfo.InvariantCulture);

                track = new MediaTrack();
                MediaTrackText.AddMediaInfoValue(track, mi.GetVideo(i, "Format"));
                MediaTrackText.AddMediaInfoValue(track, mi.GetVideo(i, "Format_Profile"));
                MediaTrackText.AddMediaInfoValue(track, mi.GetVideo(i, "Width") + "x" + mi.GetVideo(i, "Height"));
                MediaTrackText.AddMediaInfoValue(track, mi.GetVideo(i, "BitRate/String"));
                MediaTrackText.AddMediaInfoValue(track, fps + " FPS");
                MediaTrackText.AddMediaInfoValue(track, (videoCount > 1 && mi.GetVideo(i, "Default") == "Yes") ? _("Default") : "");
                track.Text = "V: " + track.Text.Trim(' ', ',');
                track.Type = "v";
                track.ID = i + 1;
                tracks.Add(track);
            }

            int audioCount = mi.GetCount(MediaInfoStreamKind.Audio);

            for (int i = 0; i < audioCount; i++)
            {
                string lang = mi.GetAudio(i, "Language/String");
                string nativeLang = GetNativeLanguage(lang);
                string? title = mi.GetAudio(i, "Title");
                string format = mi.GetAudio(i, "Format");

                if (!string.IsNullOrEmpty(title))
                {
                    if (title.ContainsEx("DTS-HD MA"))
                        format = "DTS-MA";

                    if (title.ContainsEx("DTS-HD MA"))
                        title = title.Replace("DTS-HD MA", "");

                    if (title.ContainsEx("Blu-ray"))
                        title = title.Replace("Blu-ray", "");

                    if (title.ContainsEx("UHD "))
                        title = title.Replace("UHD ", "");

                    if (title.ContainsEx("EAC"))
                        title = title.Replace("EAC", "E-AC");

                    if (title.ContainsEx("AC3"))
                        title = title.Replace("AC3", "AC-3");

                    if (title.ContainsEx(lang))
                        title = title.Replace(lang, "").Trim();

                    if (title.ContainsEx(nativeLang))
                        title = title.Replace(nativeLang, "").Trim();

                    if (title.ContainsEx("Surround"))
                        title = title.Replace("Surround", "");

                    if (title.ContainsEx("Dolby Digital"))
                        title = title.Replace("Dolby Digital", "");

                    if (title.ContainsEx("Stereo"))
                        title = title.Replace("Stereo", "");

                    if (title.StartsWithEx(format + " "))
                        title = title.Replace(format + " ", "");

                    foreach (string i2 in new[] { "2.0", "5.1", "6.1", "7.1" })
                        if (title.ContainsEx(i2))
                            title = title.Replace(i2, "").Trim();

                    if (title.ContainsEx("@ "))
                        title = title.Replace("@ ", "");

                    if (title.ContainsEx(" @"))
                        title = title.Replace(" @", "");

                    if (title.ContainsEx("()"))
                        title = title.Replace("()", "");

                    if (title.ContainsEx("[]"))
                        title = title.Replace("[]", "");

                    if (title.TrimEx() == format)
                        title = null;

                    if (!string.IsNullOrEmpty(title))
                        title = title.Trim(" _-".ToCharArray());
                }

                track = new MediaTrack();
                track.Language = lang;
                MediaTrackText.AddMediaInfoValue(track, lang);
                MediaTrackText.AddMediaInfoValue(track, format);
                MediaTrackText.AddMediaInfoValue(track, mi.GetAudio(i, "Format_Profile"));
                MediaTrackText.AddMediaInfoValue(track, mi.GetAudio(i, "BitRate/String"));
                MediaTrackText.AddMediaInfoValue(track, mi.GetAudio(i, "Channel(s)") + " ch");
                MediaTrackText.AddMediaInfoValue(track, mi.GetAudio(i, "SamplingRate/String"));
                MediaTrackText.AddMediaInfoValue(track, mi.GetAudio(i, "Forced") == "Yes" ? _("Forced") : "");
                MediaTrackText.AddMediaInfoValue(track, (audioCount > 1 && mi.GetAudio(i, "Default") == "Yes") ? _("Default") : "");
                MediaTrackText.AddMediaInfoValue(track, title);

                if (track.Text.Contains("MPEG Audio, Layer 2"))
                    track.Text = track.Text.Replace("MPEG Audio, Layer 2", "MP2");

                if (track.Text.Contains("MPEG Audio, Layer 3"))
                    track.Text = track.Text.Replace("MPEG Audio, Layer 2", "MP3");

                track.Text = "A: " + track.Text.Trim(' ', ',');
                track.Type = "a";
                track.ID = i + 1;
                tracks.Add(track);
            }

            int subCount = mi.GetCount(MediaInfoStreamKind.Text);

            for (int i = 0; i < subCount; i++)
            {
                string codec = mi.GetText(i, "Format").ToUpperEx();

                if (codec == "UTF-8")
                    codec = "SRT";
                else if (codec == "WEBVTT")
                    codec = "VTT";
                else if (codec == "VOBSUB")
                    codec = "VOB";

                string lang = mi.GetText(i, "Language/String");
                string nativeLang = GetNativeLanguage(lang);
                string title = mi.GetText(i, "Title");
                bool forced = mi.GetText(i, "Forced") == "Yes";

                if (!string.IsNullOrEmpty(title))
                {
                    if (title.ContainsEx("VobSub"))
                        title = title.Replace("VobSub", "VOB");

                    if (title.ContainsEx(codec))
                        title = title.Replace(codec, "");

                    if (title.ContainsEx(lang.ToLowerEx()))
                        title = title.Replace(lang.ToLowerEx(), lang);

                    if (title.ContainsEx(nativeLang.ToLowerEx()))
                        title = title.Replace(nativeLang.ToLowerEx(), nativeLang).Trim();

                    if (title.ContainsEx(lang))
                        title = title.Replace(lang, "");

                    if (title.ContainsEx(nativeLang))
                        title = title.Replace(nativeLang, "").Trim();

                    if (title.ContainsEx("full"))
                        title = title.Replace("full", "").Trim();

                    if (title.ContainsEx("Full"))
                        title = title.Replace("Full", "").Trim();

                    if (title.ContainsEx("Subtitles"))
                        title = title.Replace("Subtitles", "").Trim();

                    if (title.ContainsEx("forced"))
                        title = title.Replace("forced", "Forced").Trim();

                    if (forced && title.ContainsEx("Forced"))
                        title = title.Replace("Forced", "").Trim();

                    if (title.ContainsEx("()"))
                        title = title.Replace("()", "");

                    if (title.ContainsEx("[]"))
                        title = title.Replace("[]", "");

                    if (!string.IsNullOrEmpty(title))
                        title = title.Trim(" _-".ToCharArray());
                }

                track = new MediaTrack();
                track.Language = lang;
                MediaTrackText.AddMediaInfoValue(track, lang);
                MediaTrackText.AddMediaInfoValue(track, codec);
                MediaTrackText.AddMediaInfoValue(track, mi.GetText(i, "Format_Profile"));
                MediaTrackText.AddMediaInfoValue(track, forced ? _("Forced") : "");
                MediaTrackText.AddMediaInfoValue(track, (subCount > 1 && mi.GetText(i, "Default") == "Yes") ? _("Default") : "");
                MediaTrackText.AddMediaInfoValue(track, title);
                track.Text = "S: " + track.Text.Trim(' ', ',');
                track.Type = "s";
                track.ID = i + 1;
                tracks.Add(track);
            }
        }

        int editionCount = GetPropertyInt("edition-list/count");

        for (int i = 0; i < editionCount; i++)
        {
            string title = GetPropertyString($"edition-list/{i}/title");

            if (string.IsNullOrEmpty(title))
                title = _("Edition") + " " + i;

            MediaTrack track = new MediaTrack
            {
                Text = "E: " + title,
                Type = "e",
                ID = i
            };

            tracks.Add(track);
        }

        return tracks;

    }

    string[]? _profileNames;

}
