using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MpvNet;
using MpvNet.Help;
using MpvNet.Native;

Translator.Current = new TestTranslator();

string tempMediaFile = Path.Combine(Path.GetTempPath(), "mpvnet-tests-empty-media.mkv");
File.WriteAllText(tempMediaFile, "");
string tempPlaylistDir = Path.Combine(Path.GetTempPath(), "mpvnet-playlist-tests");
Directory.CreateDirectory(tempPlaylistDir);
string tempAudio = Path.Combine(tempPlaylistDir, "audio.mp3");
string tempVideo = Path.Combine(tempPlaylistDir, "video.mp4");
string tempM3u = Path.Combine(tempPlaylistDir, "playlist.m3u");
string tempPls = Path.Combine(tempPlaylistDir, "playlist.pls");
string tempXspf = Path.Combine(tempPlaylistDir, "playlist.xspf");
string tempAsx = Path.Combine(tempPlaylistDir, "playlist.asx");
string tempWpl = Path.Combine(tempPlaylistDir, "playlist.wpl");
string tempCue = Path.Combine(tempPlaylistDir, "playlist.cue");
string tempJspf = Path.Combine(tempPlaylistDir, "playlist.jspf");
string tempUnknown = Path.Combine(tempPlaylistDir, "ignored.txt");
string tempImage = Path.Combine(tempPlaylistDir, "image.jpg");
string tempSecondImage = Path.Combine(tempPlaylistDir, "second.png");
string tempVideoWithSpaces = Path.Combine(tempPlaylistDir, "video com espacos.mp4");
string tempCustomInputConf = Path.Combine(tempPlaylistDir, "custom-input.conf");
string relativeMediaFile = $"mpvnet-tests-relative-media-{Guid.NewGuid():N}.mkv";
File.WriteAllText(tempAudio, "");
File.WriteAllText(tempVideo, "");
File.WriteAllText(tempUnknown, "");
File.WriteAllText(tempImage, "");
File.WriteAllText(tempSecondImage, "");
File.WriteAllText(tempVideoWithSpaces, "");
File.WriteAllText(relativeMediaFile, "");
File.WriteAllText(tempCustomInputConf, """
x script-message-to mpvnet custom-action #menu: Custom > Custom Item
""");
File.WriteAllLines(tempM3u, [
    "#EXTM3U",
    "#EXTINF:-1,Audio title",
    "audio.mp3",
    "#EXTINF:-1,Duplicate title",
    "audio.mp3",
    "#EXTINF:-1,Video title",
    "video.mp4",
    "subtitle.srt"]);
File.WriteAllLines(tempPls, [
    "[playlist]",
    "File1=video.mp4",
    "Title1=PLS video title",
    "Length1=-1",
    "NumberOfEntries=1"]);
File.WriteAllText(tempXspf, """
<?xml version="1.0" encoding="UTF-8"?>
<playlist version="1" xmlns="http://xspf.org/ns/0/">
  <trackList>
    <track>
      <location>audio.mp3</location>
      <title>XSPF audio title</title>
    </track>
  </trackList>
</playlist>
""");
File.WriteAllText(tempAsx, """
<asx version="3.0">
  <entry>
    <title>ASX video title</title>
    <ref href="video.mp4" />
  </entry>
</asx>
""");
File.WriteAllText(tempWpl, """
<smil>
  <body>
    <seq>
      <media src="audio.mp3" title="WPL audio title" />
    </seq>
  </body>
</smil>
""");
File.WriteAllLines(tempCue, [
    "TITLE \"CUE audio title\"",
    "FILE \"audio.mp3\" MP3"]);
File.WriteAllText(tempJspf, """
{
  "playlist": {
    "track": [
      {
        "title": "JSPF video title",
        "location": ["video.mp4"]
      }
    ]
  }
}
""");
var parsedPlaylist = PlaylistFile.Read(tempM3u);
string tempNormalizedM3u = PlaylistFile.WriteTempM3u(parsedPlaylist);
string normalizedM3uContent = File.ReadAllText(tempNormalizedM3u);
var parsedPlsPlaylist = PlaylistFile.Read(tempPls);
string tempNormalizedPlsM3u = PlaylistFile.WriteTempM3u(parsedPlsPlaylist);
string normalizedPlsM3uContent = File.ReadAllText(tempNormalizedPlsM3u);
var parsedXspfPlaylist = PlaylistFile.Read(tempXspf);
var parsedAsxPlaylist = PlaylistFile.Read(tempAsx);
var parsedWplPlaylist = PlaylistFile.Read(tempWpl);
var parsedCuePlaylist = PlaylistFile.Read(tempCue);
var parsedJspfPlaylist = PlaylistFile.Read(tempJspf);
var parsedConfig = ConfigFileParser.ParseKeyValueLines([
    "#ignored=value",
    "missing-separator",
    "dark-mode = never",
    "language= pt-BR ",
    "--fullscreen=yes",
    "path = C:\\Media=WithEquals\\video.mkv",
    "duplicate=old",
    "duplicate=new"]);
var parsedCommandArguments = CommandLine.ParseArguments([
    "video.mp4",
    "--terminal",
    "--no-config",
    "--script=test.lua",
    "--script-opt=thumbfast=yes",
    "--audio-file=audio.mp3",
    "--sub-file=sub.srt",
    "--external-file=cover.jpg",
    "--title=sample.video.mkv",
    "--force-media-title=forced.title.mp4",
    "--title=${media-title}",
    "--=ignored"]);
var parsedChangeListArguments = CommandLine.ParseArguments([
    "--script-opts-add=osc-layout=bottombar",
    "--script-opts-set=osc-layout=box",
    "--script-opts-append=thumbfast=yes",
    "--script-opts-pre=first=yes",
    "--script-opts-clr",
    "--script-opts-remove=old=yes",
    "--script-opts-toggle=flag"]);
string commandLinePlaylistTitle = CommandLine.GetCommandLinePlaylistTitle(parsedCommandArguments);
string templateOnlyPlaylistTitle = CommandLine.GetCommandLinePlaylistTitle(
    CommandLine.ParseArguments(["--title=${media-title}"]));
var separatedTitleArguments = CommandLine.ParseArguments(["--title", "Nome do vídeo", "https://example.com/video.mp4"]);
var titleAndUrlRequest = CommandLine.ResolveMediaRequest(
    ["Nome do vídeo", "https://example.com/video.mp4"],
    CommandLine.ParseArguments(["Nome do vídeo", "https://example.com/video.mp4"]));
var separatedTitleRequest = CommandLine.ResolveMediaRequest(
    ["--title", "Nome do vídeo", "https://example.com/video.mp4"],
    separatedTitleArguments);
var directUrlRequest = CommandLine.ResolveMediaRequest(
    ["https://example.com/video.mp4"],
    CommandLine.ParseArguments(["https://example.com/video.mp4"]));
var directFileRequest = CommandLine.ResolveMediaRequest(
    [tempVideo],
    CommandLine.ParseArguments([tempVideo]));
var multipleFilesRequest = CommandLine.ResolveMediaRequest(
    [tempAudio, tempVideo],
    CommandLine.ParseArguments([tempAudio, tempVideo]));
var invalidTitleUrlRequest = CommandLine.ResolveMediaRequest(
    ["@#$", "https://example.com/video.mp4"],
    CommandLine.ParseArguments(["@#$", "https://example.com/video.mp4"]));
var queryUrlRequest = CommandLine.ResolveMediaRequest(
    ["https://example.com/live/index.m3u8?token=secret&name=video"],
    CommandLine.ParseArguments(["https://example.com/live/index.m3u8?token=secret&name=video"]));
var escapedUrlRequest = CommandLine.ResolveMediaRequest(
    ["https://example.com/video%20com%20espacos.mp4"],
    CommandLine.ParseArguments(["https://example.com/video%20com%20espacos.mp4"]));
var invalidUrlRequest = CommandLine.ResolveMediaRequest(
    ["not-a-valid-url"],
    CommandLine.ParseArguments(["not-a-valid-url"]));
var activeBindings = InputHelp.GetActiveBindings([
    new Binding(command: "cycle pause", input: "SPACE"),
    new Binding(command: "cycle pause", input: "p"),
    new Binding(command: "ignored", input: ""),
    new Binding(command: "", input: "x")]);
var duplicateInputActiveBindings = InputHelp.GetActiveBindings([
    new Binding(command: "first command", input: "X"),
    new Binding(command: "second command", input: "X")]);
var normalizedModifierBindings = InputHelp.Parse("ctrl+shift+alt+x cycle pause # Modified Pause");
var defaultMenuLabels = new Dictionary<string, string>();
foreach (Binding binding in InputHelp.GetDefaults())
{
    if (!binding.IsMenu || string.IsNullOrWhiteSpace(binding.Command))
        continue;

    defaultMenuLabels.TryAdd(binding.Command, binding.Comment);
}
string pauseBindings = InputHelp.GetBindingsForCommand(activeBindings, "cycle pause");
var customMenuBindings = new InputConf(tempCustomInputConf).GetBindings().menuBindings;
var languageNormalizationCases = new (string Input, string Expected)[]
{
    ("eng", "en"),
    ("en_US", "en-US"),
    ("en-GB", "en-GB"),
    ("English", "en"),
    ("por", "pt"),
    ("pt_BR", "pt-BR"),
    ("pt_PT", "pt-PT"),
    ("Portuguese", "pt"),
    ("Português", "pt"),
    ("Brazilian Portuguese", "pt-BR"),
    ("Português do Brasil", "pt-BR"),
    ("Portuguese Portugal", "pt-PT"),
    ("Português de Portugal", "pt-PT"),
    ("spa", "es"),
    ("es_MX", "es-MX"),
    ("Spanish", "es"),
    ("Español", "es"),
    ("fra", "fr"),
    ("fre", "fr"),
    ("fr_CA", "fr-CA"),
    ("chi", "zh"),
    ("zho", "zh"),
    ("zh_CN", "zh-CN"),
    ("zh_TW", "zh-TW"),
    ("Simplified Chinese", "zh-CN"),
    ("Traditional Chinese", "zh-TW"),
    ("jpn", "ja"),
    ("Japanese", "ja"),
    ("deu", "de"),
    ("ger", "de"),
    ("German", "de"),
    ("ita", "it"),
    ("Italian", "it"),
};
var interfaceFallbackAvailable = new[] { "en", "es", "es-419", "pt", "fr", "zh-CN", "zh-TW", "de" };
var mediaLanguageTracks = new[]
{
    new MediaTrack { ID = 1, Type = "a", Language = "en" },
    new MediaTrack { ID = 2, Type = "a", Language = "pt" },
    new MediaTrack { ID = 3, Type = "a", Language = "pt-BR" },
    new MediaTrack { ID = 4, Type = "a", Language = "es-419" },
    new MediaTrack { ID = 5, Type = "s", Language = "zh-TW" },
    new MediaTrack { ID = 6, Type = "s", Language = "zh-CN" },
    new MediaTrack { ID = 7, Type = "s", Language = "sr-Latn" },
    new MediaTrack { ID = 8, Type = "s", Language = "sr-Cyrl" },
    new MediaTrack { ID = 9, Type = "s", Language = "pt-PT" },
    new MediaTrack { ID = 10, Type = "s", Language = "pt" },
};

DateTime fixedLogDate = new(2026, 6, 2, 19, 45, 10, 123);
string tempLogDir = Path.Combine(Path.GetTempPath(), "mpvnet-log-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempLogDir);
File.WriteAllText(Path.Combine(tempLogDir, "mpvnet-2026-05-29.log"), "old");
File.WriteAllText(Path.Combine(tempLogDir, "mpvnet-2026-05-30.log"), "keep");
File.WriteAllText(Path.Combine(tempLogDir, "other-2026-05-01.log"), "unrelated");
var logWriter = new FileLogWriter(tempLogDir, () => fixedLogDate);
logWriter.Write(LogLevel.Info, "info message", null);
logWriter.Write(LogLevel.Debug, "debug message", null);
logWriter.Write(LogLevel.Error, "error message", new InvalidOperationException("outer", new Exception("inner")));
string dailyLogFile = Path.Combine(tempLogDir, "mpvnet-2026-06-02.log");
string dailyLogContent = File.ReadAllText(dailyLogFile);
string safeUrlWithSecret = Log.SafeValue("https://example.com/live/index.m3u8?token=secret#fragment");
string safePlainUrl = Log.SafeValue("https://example.com/live/index.m3u8");
string blockedLogPath = Path.Combine(tempLogDir, "blocked");
File.WriteAllText(blockedLogPath, "");
var blockedLogWriter = new FileLogWriter(blockedLogPath, () => fixedLogDate);
bool blockedWriteDidNotThrow = true;
string expectedLocalAppDataRoot = Path.Combine(Folder.LocalAppData, "mpv.net");
string defaultCacheFolder = new MainPlayer().CacheFolder;
string defaultTempFolder = Global.App.TempFolder;

DateTime fixedCleanupDate = new(2026, 6, 2, 12, 0, 0);
string tempCleanupDir = Path.Combine(Path.GetTempPath(), "mpvnet-cleanup-tests-" + Guid.NewGuid().ToString("N"));
string cleanupCacheDir = Path.Combine(tempCleanupDir, "Cache");
string cleanupTempDir = Path.Combine(tempCleanupDir, "Temp");
Directory.CreateDirectory(cleanupCacheDir);
Directory.CreateDirectory(cleanupTempDir);
string oldCacheFile = Path.Combine(cleanupCacheDir, "old-cache.bin");
string recentCacheFile = Path.Combine(cleanupCacheDir, "recent-cache.bin");
string oldTempFile = Path.Combine(cleanupTempDir, "old-temp.m3u8");
string recentTempFile = Path.Combine(cleanupTempDir, "recent-temp.m3u8");
string oldEmptyDir = Path.Combine(cleanupCacheDir, "old-empty");
Directory.CreateDirectory(oldEmptyDir);
File.WriteAllText(oldCacheFile, "old");
File.WriteAllText(recentCacheFile, "recent");
File.WriteAllText(oldTempFile, "old");
File.WriteAllText(recentTempFile, "recent");
File.SetLastWriteTime(oldCacheFile, fixedCleanupDate.AddDays(-4));
File.SetLastWriteTime(oldTempFile, fixedCleanupDate.AddDays(-4));
File.SetLastWriteTime(recentCacheFile, fixedCleanupDate.AddDays(-1));
File.SetLastWriteTime(recentTempFile, fixedCleanupDate.AddDays(-1));
Directory.SetLastWriteTime(oldEmptyDir, fixedCleanupDate.AddDays(-4));
TemporaryFileCleanup.Cleanup(fixedCleanupDate, cleanupCacheDir, cleanupTempDir);
bool missingCleanupFolderDidNotThrow = true;

try
{
    TemporaryFileCleanup.Cleanup(fixedCleanupDate, Path.Combine(tempCleanupDir, "missing"));
}
catch
{
    missingCleanupFolderDidNotThrow = false;
}

try
{
    blockedLogWriter.Write(LogLevel.Info, "ignored", null);
}
catch
{
    blockedWriteDidNotThrow = false;
}

var normalizedRemotePlaylistItems = PlaylistFile.Normalize(tempM3u, [
    new PlaylistFileItem("https://example.com/live/index.m3u8?token=abc", "remote live")]);
var normalizedFileUriPlaylistItems = PlaylistFile.Normalize(tempM3u, [
    new PlaylistFileItem(new Uri(tempAudio).AbsoluteUri, "file uri audio")]);
var normalizedQuotedPlaylistItems = PlaylistFile.Normalize(tempM3u, [
    new PlaylistFileItem(tempVideo, "\"quoted\" 'video' title.mp4")]);
var normalizedAutocreatedPlaylistItems = PlaylistFile.NormalizeDisplayTitles([
    new PlaylistFileItem(tempVideo, "Vue.js parte 2 Aula 1 - Atividade 3 Criando Nossa Primeira Diretiva Alura Cursos Online De Tecnologia.mp4")]);
string tempRawTitleM3u = PlaylistFile.WriteTempM3u([
    new PlaylistFileItem(tempVideo, "\"raw\" 'playlist' item.mp4")]);
string rawTitleM3uContent = File.ReadAllText(tempRawTitleM3u);
string tempAtomicWriteFile = Path.Combine(tempPlaylistDir, "atomic-write.txt");
string tempAtomicNestedFile = Path.Combine(tempPlaylistDir, "nested", "atomic-write.txt");
File.WriteAllText(tempAtomicWriteFile, "old");
FileHelp.WriteAllTextAtomic(tempAtomicWriteFile, "new");
FileHelp.WriteAllTextAtomic(tempAtomicNestedFile, "created");
string atomicWriteContent = File.ReadAllText(tempAtomicWriteFile);
string atomicNestedWriteContent = File.ReadAllText(tempAtomicNestedFile);
bool atomicWriteCleanedTempFiles = !Directory.EnumerateFiles(tempPlaylistDir, "atomic-write.txt.*.tmp").Any();

MediaTrack mpvTrackText = new();
MediaTrackText.AddMpvValue(mpvTrackText, " AAC ");
MediaTrackText.AddMpvValue(mpvTrackText, "AAC");
MediaTrackText.AddMpvValue(mpvTrackText, null);

MediaTrack mediaInfoTrackText = new();
MediaTrackText.AddMediaInfoValue(mediaInfoTrackText, " DTS ");
MediaTrackText.AddMediaInfoValue(mediaInfoTrackText, "DTS");
MediaTrackText.AddMediaInfoValue(mediaInfoTrackText, null);

string[] expectedAudioExts = [
    "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "wma",
    "alac", "aiff", "aif", "ape", "wv", "mka", "ac3", "dts",
    "eac3", "amr"];

string[] legacyAudioExts = ["au", "mp2", "mpa", "mpc", "thd", "w64", "oga", "ogm", "dtshd", "dtshr", "dtsma"];

string[] audioExts = FileTypes.GetAudioExts();
string[] imageExts = FileTypes.GetImgExts();
string openFileDialogFilter = FileTypes.GetOpenFileDialogFilter();
string openFileDialogFirstFilter = openFileDialogFilter.Split('|')[1];
string[] httpStreamingLoadfileArgs = MainPlayer.BuildLoadfileArgs("https://example.com/video.mp4", 0, false);
string[] ftpStreamingLoadfileArgs = MainPlayer.BuildLoadfileArgs("ftp://example.com/video.mp4", 1, true);
string[] localLoadfileArgs = MainPlayer.BuildLoadfileArgs(tempVideo, 0, false);

var tests = new (string Name, bool Result)[]
{
    ("IsVideoFile .mp4", FileTypes.IsVideoFile(".mp4")),
    ("IsVideoFile .mkv", FileTypes.IsVideoFile(".mkv")),
    ("IsPlaylistFile .m3u8", FileTypes.IsPlaylistFile(".m3u8")),
    ("IsPlaylistFile .cue", FileTypes.IsPlaylistFile(".cue")),
    ("IsPlaylistFile .asx", FileTypes.IsPlaylistFile(".asx")),
    ("IsPlaylistFile .wpl", FileTypes.IsPlaylistFile(".wpl")),
    ("IsPlaylistFile .jspf", FileTypes.IsPlaylistFile(".jspf")),
    ("IsAudioFile .mp3", FileTypes.IsAudioFile(".mp3")),
    ("Default image extensions include avif", imageExts.Contains("avif")),
    ("Default image extensions include jpeg", imageExts.Contains("jpeg")),
    ("Default image extensions include jxl", imageExts.Contains("jxl")),
    ("Default image extensions include tiff", imageExts.Contains("tiff")),
    ("IsStreamingUrl https HLS", FileTypes.IsStreamingUrl("https://example.com/live.m3u8")),
    ("IsStreamingUrl rtmp", FileTypes.IsStreamingUrl("rtmp://server/live")),
    ("IsStreamingUrl rtsp", FileTypes.IsStreamingUrl("rtsp://server/stream")),
    ("IsStreamingUrl udp", FileTypes.IsStreamingUrl("udp://239.0.0.1:1234")),
    ("IsStreamingUrl is case-insensitive", FileTypes.IsStreamingUrl("HTTPS://example.com/live.m3u8")),
    ("URL query string supported", FileTypes.IsSupportedMediaInput("https://example.com/live/index.m3u8?token=abc123")),
    ("URL fragment supported", FileTypes.IsSupportedMediaInput("https://example.com/live/index.m3u8#stream")),
    ("Uppercase media extension supported", FileTypes.IsVideoFile("MOVIE.MKV")),
    ("Unknown file false", !FileTypes.IsSupportedMediaInput("example.unknown")),
    ("Supported media input accepts audio", FileTypes.IsSupportedMediaInput("audio.mp3")),
    ("Empty text false", !FileTypes.IsSupportedMediaInput("")),
    ("URL does not depend on File.Exists", FileTypes.IsSupportedMediaInput("https://example.com/video.mp4")),
    ("Title normalization removes extension and dot separators", TitleHelp.NormalizeMediaTitle("filme.exemplo.2024.mkv") == "Filme Exemplo 2024"),
    ("Title normalization collapses repeated spaces", TitleHelp.NormalizeMediaTitle("  arquivo..com  ..pontos.mp4  ") == "Arquivo Com Pontos"),
    ("Title normalization replaces dot comma dash and underscore with spaces", TitleHelp.NormalizeMediaTitle("um,titulo-bem_trocado.mp4") == "Um Titulo Bem Trocado"),
    ("Title normalization removes configured characters", TitleHelp.NormalizeMediaTitle("@titulo#com$simbolos*.mp4") == "Titulocomsimbolos"),
    ("Title normalization treats single and double quotes as word separators", TitleHelp.NormalizeMediaTitle("video\"aula'novo.mp4") == "Video Aula Novo"),
    ("Title normalization uses default title when empty", TitleHelp.NormalizeMediaTitle("@#$*.mp4") == "Untitled Track"),
    ("Title normalization truncates long titles", TitleHelp.NormalizeMediaTitle(new string('a', 120) + ".mp4").Length == 100),
    ("Title normalization removes mpv.net suffix", TitleHelp.NormalizeMediaTitle("movie title - mpv.net") == "Movie Title"),
    ("Title normalization removes mpv suffix after pipe", TitleHelp.NormalizeMediaTitle("movie title | mpv") == "Movie Title"),
    ("Title normalization keeps unsupported extension text", TitleHelp.NormalizeMediaTitle("notes.backup") == "Notes Backup"),
    ("Command line accepts streaming URL", CommandLine.IsLoadableFileArgument("rtmps://example.com/live")),
    ("Command line accepts playlist file extension", CommandLine.IsLoadableFileArgument("iptv.m3u")),
    ("Command line accepts stdin pipe marker", CommandLine.IsLoadableFileArgument("-")),
    ("Command line rejects options as files", !CommandLine.IsLoadableFileArgument("--fullscreen")),
    ("Command line defers profile until after media loading", CommandLine.IsPostFileProperty("profile")),
    ("Command line lets idle override a deferred profile", CommandLine.IsPostProfileOverrideProperty("idle")),
    ("Command line keeps script opts init-only after profile", !CommandLine.IsPostProfileOverrideProperty("script-opts")),
    ("Command line accepts absolute Windows path without existence check", CommandLine.IsLoadableFileArgument(@"C:\missing\movie.mkv")),
    ("Command line accepts relative dot path", CommandLine.IsLoadableFileArgument(@".\movie.mkv")),
    ("Command line rejects unknown relative path", !CommandLine.IsLoadableFileArgument("missing-relative-file.unknown")),
    ("Command line accepts local path with spaces", CommandLine.IsLoadableFileArgument(tempVideoWithSpaces)),
    ("ConvertFilePath resolves existing relative file", Path.GetFullPath(MainPlayer.ConvertFilePath(relativeMediaFile)) == Path.GetFullPath(relativeMediaFile)),
    ("ConvertFilePath keeps missing relative file unchanged", MainPlayer.ConvertFilePath("missing-relative-file.mkv") == "missing-relative-file.mkv"),
    ("ConvertFilePath normalizes mixed Windows separators", MainPlayer.ConvertFilePath("C:/Media/video.mkv") == @"C:\Media\video.mkv"),
    ("Local file can use optional MediaInfo when present", MainPlayer.CanUseMediaInfo(tempMediaFile)),
    ("Missing local file skips optional MediaInfo", !MainPlayer.CanUseMediaInfo(tempMediaFile + ".missing")),
    ("Streaming URL skips optional MediaInfo", !MainPlayer.CanUseMediaInfo("https://example.com/live/index.m3u8")),
    ("Streaming URL uses automatic network tolerance", MainPlayer.ShouldUseAutomaticStreamingOptions("https://example.com/live/index.m3u8")),
    ("RTSP URL uses automatic network tolerance", MainPlayer.ShouldUseAutomaticStreamingOptions("rtsp://example.com/stream")),
    ("Local file skips automatic network tolerance", !MainPlayer.ShouldUseAutomaticStreamingOptions(tempVideo)),
    ("Automatic streaming options keep 60 second timeout", MainPlayer.AutomaticStreamingLoadOptions.Contains("network-timeout=60") && MainPlayer.AutomaticStreamingLoadOptions.Contains("cache-pause-wait=60")),
    ("Automatic streaming loadfile options use current mpv argument slot", MainPlayer.LoadfileOptionsInsertionIndex == "-1"),
    ("HTTP streaming loadfile passes options in fourth mpv argument", httpStreamingLoadfileArgs.SequenceEqual(["loadfile", "https://example.com/video.mp4", "replace", "-1", MainPlayer.AutomaticStreamingLoadOptions])),
    ("FTP streaming append loadfile keeps automatic network tolerance", ftpStreamingLoadfileArgs.SequenceEqual(["loadfile", "ftp://example.com/video.mp4", "append", "-1", MainPlayer.AutomaticStreamingLoadOptions])),
    ("Local loadfile keeps normal mpv arguments", localLoadfileArgs.SequenceEqual(["loadfile", tempVideo])),
    ("Pipe input skips optional MediaInfo", !MainPlayer.CanUseMediaInfo(@"\\.\pipe\mpvnet-test")),
    ("Streaming without duration is still loadable", CommandLine.IsLoadableFileArgument("https://example.com/live/no-duration")),
    ("Streaming without title is still loadable", CommandLine.IsLoadableFileArgument("rtsp://example.com/stream")),
    ("YouTube URL detection handles watch URLs", MainPlayer.IsYouTubeUrl("https://www.youtube.com/watch?v=DuVaLWf2114")),
    ("YouTube URL detection handles short URLs", MainPlayer.IsYouTubeUrl("https://youtu.be/Xy9IqrKjilg")),
    ("Non-YouTube streaming URL is not treated as YouTube", !MainPlayer.IsYouTubeUrl("https://example.com/live/index.m3u8")),
    ("Remote M3U detection accepts UTF-8 BOM", MainPlayer.LooksLikeM3u(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("#EXTM3U")).ToArray())),
    ("Remote M3U detection accepts leading whitespace", MainPlayer.LooksLikeM3u(Encoding.UTF8.GetBytes("\r\n  #EXTM3U\r\n"))),
    ("Remote M3U detection rejects normal media", !MainPlayer.LooksLikeM3u(Encoding.UTF8.GetBytes("not a playlist"))),
    ("Remote playlist probe timeout is at least 60 seconds", MainPlayer.RemotePlaylistTimeout >= TimeSpan.FromSeconds(60)),
    ("Remote playlist probe timeout is expected diagnostic", MainPlayer.IsRemotePlaylistProbeTimeout(new TaskCanceledException("request canceled", new TimeoutException("timeout")))),
    ("Remote playlist probe non-timeout stays unexpected", !MainPlayer.IsRemotePlaylistProbeTimeout(new InvalidOperationException("bad response"))),
    ("Invalid empty URL is not loadable", !CommandLine.IsLoadableFileArgument("")),
    ("Invalid unknown local path is not supported media input", !FileTypes.IsSupportedMediaInput(@"C:\missing\file.unknown")),
    ("Audio defaults keep legacy formats", legacyAudioExts.All(audioExts.Contains)),
    ("Audio defaults add modern formats", expectedAudioExts.All(audioExts.Contains)),
    ("Open file dialog first filter includes video audio and playlists", openFileDialogFirstFilter.Contains("*.mp4") && openFileDialogFirstFilter.Contains("*.mp3") && openFileDialogFirstFilter.Contains("*.m3u8")),
    ("Open file dialog keeps separate playlist filter", openFileDialogFilter.Contains("|Playlists|*.m3u;*.m3u8;*.pls;*.xspf;*.asx;*.wpl;*.cue;*.jspf|")),
    ("Folder media filter includes playlists", FileTypes.GetMediaFiles([tempAudio, tempVideo, tempM3u, tempUnknown]).Count() == 3),
    ("Folder media filter keeps playlist files", FileTypes.GetMediaFiles([tempM3u]).Single() == tempM3u),
    ("Folder autoload for video includes audio video and playlists", FileTypes.GetFolderMediaFiles([tempAudio, tempVideo, tempM3u, tempImage, tempUnknown], tempVideo).SequenceEqual([tempAudio, tempVideo, tempM3u])),
    ("Folder autoload for image includes only images", FileTypes.GetFolderMediaFiles([tempAudio, tempVideo, tempM3u, tempImage, tempSecondImage, tempUnknown], tempImage).SequenceEqual([tempImage, tempSecondImage])),
    ("Folder autoload detects uppercase image extension", FileTypes.GetFolderMediaFiles([tempAudio, tempImage.ToUpperInvariant()], tempImage.ToUpperInvariant()).Single() == tempImage.ToUpperInvariant()),
    ("Empty media track defaults avoid null bindings", new MediaTrack().Text == "" && new MediaTrack().Language == ""),
    ("Playlist parser keeps playable unique items", parsedPlaylist.Count == 2),
    ("Playlist parser resolves relative media paths", parsedPlaylist.Any(i => i.Path == tempAudio)),
    ("Playlist parser normalizes item title", parsedPlaylist.Any(i => i.Title == "Video Title" && i.Path == tempVideo)),
    ("Playlist writer preserves normalized item titles", normalizedM3uContent.Contains("#EXTINF:-1,Video Title")),
    ("Playlist writer preserves resolved paths", normalizedM3uContent.Contains(tempVideo)),
    ("Playlist normalizer keeps streaming URLs", normalizedRemotePlaylistItems.Single().Path == "https://example.com/live/index.m3u8?token=abc"),
    ("Playlist normalizer resolves file URIs", Path.GetFullPath(normalizedFileUriPlaylistItems.Single().Path) == Path.GetFullPath(tempAudio)),
    ("Playlist normalizer removes quotes from titles", normalizedQuotedPlaylistItems.Single().Title == "Quoted Video Title"),
    ("Autocreated playlist title normalization removes extension", normalizedAutocreatedPlaylistItems.Single().Title == "Vue Js Parte 2 Aula 1 Atividade 3 Criando Nossa Primeira Diretiva Alura Cursos Online De Tecnologia"),
    ("Playlist writer normalizes raw item titles", rawTitleM3uContent.Contains("#EXTINF:-1,Raw Playlist Item")),
    ("Atomic text write replaces existing content", atomicWriteContent == "new"),
    ("Atomic text write creates missing folders", atomicNestedWriteContent == "created"),
    ("Atomic text write removes temporary file", atomicWriteCleanedTempFiles),
    ("PLS parser normalizes item title", parsedPlsPlaylist.Any(i => i.Title == "Pls Video Title" && i.Path == tempVideo)),
    ("PLS writer preserves normalized item titles", normalizedPlsM3uContent.Contains("#EXTINF:-1,Pls Video Title")),
    ("XSPF parser resolves relative media paths", parsedXspfPlaylist.Single().Path == tempAudio),
    ("XSPF parser normalizes item title", parsedXspfPlaylist.Single().Title == "Xspf Audio Title"),
    ("ASX parser resolves relative media paths", parsedAsxPlaylist.Single().Path == tempVideo),
    ("ASX parser normalizes item title", parsedAsxPlaylist.Single().Title == "Asx Video Title"),
    ("WPL parser resolves relative media paths", parsedWplPlaylist.Single().Path == tempAudio),
    ("WPL parser normalizes item title", parsedWplPlaylist.Single().Title == "Wpl Audio Title"),
    ("CUE parser resolves relative media paths", parsedCuePlaylist.Single().Path == tempAudio),
    ("CUE parser normalizes item title", parsedCuePlaylist.Single().Title == "Cue Audio Title"),
    ("JSPF parser resolves array location", parsedJspfPlaylist.Single().Path == tempVideo),
    ("JSPF parser normalizes item title", parsedJspfPlaylist.Single().Title == "Jspf Video Title"),
    ("Config parser skips comments and invalid lines", parsedConfig.Count == 5),
    ("Config parser trims keys and values", parsedConfig["dark-mode"] == "never" && parsedConfig["language"] == "pt-BR"),
    ("Config parser keeps leading option dashes", parsedConfig["--fullscreen"] == "yes"),
    ("Config parser preserves equals in values", parsedConfig["path"] == @"C:\Media=WithEquals\video.mkv"),
    ("Config parser keeps last duplicate value", parsedConfig["duplicate"] == "new"),
    ("Language normalizer handles expected aliases", languageNormalizationCases.All(test => LanguageNormalizer.Normalize(test.Input) == test.Expected)),
    ("Interface fallback en-US falls back to en", LanguageFallbackResolver.GetFallbacks("en-US", interfaceFallbackAvailable).SequenceEqual(["en"])),
    ("Interface fallback en-GB falls back to en", LanguageFallbackResolver.GetFallbacks("en-GB", interfaceFallbackAvailable).SequenceEqual(["en"])),
    ("Interface fallback es-MX tries es-419 before es", LanguageFallbackResolver.GetFallbacks("es-MX", interfaceFallbackAvailable).SequenceEqual(["es-419", "es", "en"])),
    ("Interface fallback es-ES falls back to es", LanguageFallbackResolver.GetFallbacks("es-ES", interfaceFallbackAvailable).SequenceEqual(["es", "en"])),
    ("Interface fallback pt-BR falls back to pt", LanguageFallbackResolver.GetFallbacks("pt-BR", interfaceFallbackAvailable).SequenceEqual(["pt", "en"])),
    ("Interface fallback pt-PT falls back to pt", LanguageFallbackResolver.GetFallbacks("pt-PT", interfaceFallbackAvailable).SequenceEqual(["pt", "en"])),
    ("Interface fallback fr-CA falls back to fr", LanguageFallbackResolver.GetFallbacks("fr-CA", interfaceFallbackAvailable).SequenceEqual(["fr", "en"])),
    ("Interface fallback zh-CN keeps zh-CN", LanguageFallbackResolver.GetFallbacks("zh-CN", interfaceFallbackAvailable).SequenceEqual(["zh-CN", "en"])),
    ("Interface fallback zh-TW keeps zh-TW", LanguageFallbackResolver.GetFallbacks("zh-TW", interfaceFallbackAvailable).SequenceEqual(["zh-TW", "en"])),
    ("Interface fallback de-DE falls back to de", LanguageFallbackResolver.GetFallbacks("de-DE", interfaceFallbackAvailable).SequenceEqual(["de", "en"])),
    ("Interface unknown language falls back to default", LanguageFallbackResolver.GetFallbacks("xx-ZZ", interfaceFallbackAvailable).SequenceEqual(["en"])),
    ("Manual interface language keeps the selected value", LocalizationService.ResolveMpvNetLanguage("portuguese-brazil", new("en-US")) == "portuguese-brazil"),
    ("Startup language uses supported Windows culture", LocalizationService.ResolveStartupLanguage(new("pt-PT")) == "portuguese-portugal"),
    ("Startup language falls back to english", LocalizationService.ResolveStartupLanguage(new("xx-ZZ")) == "english"),
    ("Alang interface preference uses first supported language", LocalizationService.ResolveFromMpvLanguageList("jpn,por", new("en-US")) == "japanese"),
    ("Alang unknown language falls back to default language", LocalizationService.ResolveFromMpvLanguageList("xx-ZZ", new("pt-PT")) == "english"),
    ("Audio selects exact pt-BR when available", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "a", "pt-BR") == 3),
    ("Audio falls back from pt-PT to pt", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "a", "pt-PT") == 2),
    ("Audio selects exact en when en-US falls back", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "a", "en-US") == 1),
    ("Audio es-MX falls back to es-419", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "a", "es-MX") == 4),
    ("Audio keeps mpv default when no language matches", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "a", "de-DE") == null),
    ("Audio manual preference returns manual track id", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "a", "pt-BR", mode: MediaLanguageSelectionMode.Manual, manualTrackId: 1) == 1),
    ("Subtitle disabled mode stays disabled", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "s", "pt-BR", mode: MediaLanguageSelectionMode.Disabled) == null),
    ("Subtitle selects exact pt-PT when available", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "s", "pt-PT") == 9),
    ("Subtitle pt-BR falls back to pt", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks, "s", "pt-BR") == 10),
    ("Subtitle does not select zh-TW for zh-CN", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks.Where(track => track.ID != 6), "s", "zh-CN") == null),
    ("Subtitle does not select zh-CN for zh-TW", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks.Where(track => track.ID != 5), "s", "zh-TW") == null),
    ("Subtitle does not select sr-Latn for sr-Cyrl", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks.Where(track => track.ID != 8), "s", "sr-Cyrl") == null),
    ("Subtitle does not select sr-Cyrl for sr-Latn", MediaLanguageService.SelectPreferredTrack(mediaLanguageTracks.Where(track => track.ID != 7), "s", "sr-Latn") == null),
    ("Command parser ignores non-options and empty names", parsedCommandArguments.Count == 10),
    ("Command parser handles boolean flags", parsedCommandArguments.Any(i => i.Name == "terminal" && i.Value == "yes")),
    ("Command parser handles no-prefix flags", parsedCommandArguments.Any(i => i.Name == "config" && i.Value == "no")),
    ("Command parser normalizes aliases", parsedCommandArguments.Any(i => i.Name == "scripts" && i.Value == "test.lua") &&
        parsedCommandArguments.Any(i => i.Name == "script-opts" && i.Value == "thumbfast=yes") &&
        parsedCommandArguments.Any(i => i.Name == "audio-files" && i.Value == "audio.mp3") &&
        parsedCommandArguments.Any(i => i.Name == "sub-files" && i.Value == "sub.srt") &&
        parsedCommandArguments.Any(i => i.Name == "external-files" && i.Value == "cover.jpg")),
    ("Command parser normalizes explicit title values", parsedCommandArguments.Any(i => i.Name == "title" && i.Value == "Sample Video")),
    ("Command parser consumes separated title values", separatedTitleArguments.Any(i => i.Name == "title" && i.Value == "Nome Do Vídeo")),
    ("Command parser consumes separated force media title", CommandLine.ParseArguments(["--force-media-title", "canal ao vivo"]).Single(i => i.Name == "force-media-title").Value == "Canal Ao Vivo"),
    ("Command parser preserves title templates", parsedCommandArguments.Any(i => i.Name == "title" && i.Value == "${media-title}")),
    ("Command parser normalizes force media title", parsedCommandArguments.Any(i => i.Name == "force-media-title" && i.Value == "Forced Title")),
    ("Command parser uses force media title for URL playlist title", commandLinePlaylistTitle == "Forced Title"),
    ("Command parser ignores title templates for URL playlist title", templateOnlyPlaylistTitle == ""),
    ("Command line title metadata keeps direct URL media", separatedTitleRequest.Files.SequenceEqual(["https://example.com/video.mp4"]) && separatedTitleRequest.Title == "Nome Do Vídeo"),
    ("Command line name plus URL resolves URL as primary media", titleAndUrlRequest.PrimaryMedia == "https://example.com/video.mp4" && titleAndUrlRequest.Title == "Nome Do Vídeo"),
    ("Command line separated title plus URL resolves URL as primary media", separatedTitleRequest.PrimaryMedia == "https://example.com/video.mp4" && separatedTitleRequest.Title == "Nome Do Vídeo"),
    ("Command line direct URL resolves without playlist dependency", directUrlRequest.Files.SequenceEqual(["https://example.com/video.mp4"]) && directUrlRequest.Title == ""),
    ("Command line direct local file resolves without playlist dependency", directFileRequest.Files.Single() == tempVideo && directFileRequest.PrimaryMedia == tempVideo),
    ("Command line multiple files keeps order", multipleFilesRequest.Files.SequenceEqual([tempAudio, tempVideo])),
    ("Command line invalid title still preserves valid URL", invalidTitleUrlRequest.PrimaryMedia == "https://example.com/video.mp4" && invalidTitleUrlRequest.Title == "Untitled Track"),
    ("Command line IPTV URL resolves as primary media", CommandLine.ResolveMediaRequest(["https://example.com/live/index.m3u8"], []).PrimaryMedia == "https://example.com/live/index.m3u8"),
    ("Command line URL with query string resolves as primary media", queryUrlRequest.PrimaryMedia == "https://example.com/live/index.m3u8?token=secret&name=video"),
    ("Command line escaped URL resolves as primary media", escapedUrlRequest.PrimaryMedia == "https://example.com/video%20com%20espacos.mp4"),
    ("Command line invalid URL is not selected as media", invalidUrlRequest.Files.Count == 0 && invalidUrlRequest.PrimaryMedia == ""),
    ("Command line title does not replace raw primary media", titleAndUrlRequest.Files.SequenceEqual(["https://example.com/video.mp4"]) && titleAndUrlRequest.PrimaryMedia == "https://example.com/video.mp4"),
    ("Command parser keeps change-list operation names", parsedChangeListArguments.Select(i => i.Name).SequenceEqual([
        "script-opts-add",
        "script-opts-set",
        "script-opts-append",
        "script-opts-pre",
        "script-opts-clr",
        "script-opts-remove",
        "script-opts-toggle"])),
    ("Input bindings ignore incomplete entries", activeBindings.Count == 2),
    ("Input bindings keep last command for duplicate key", duplicateInputActiveBindings["X"].Command == "second command"),
    ("Input parser normalizes shortcut modifiers", normalizedModifierBindings.Single().Input == "Ctrl+Shift+Alt+x"),
    ("Input bindings list keys for command", pauseBindings == "SPACE, p"),
    ("Default menu labels keep first playlist menu path", defaultMenuLabels["script-binding select/select-playlist"] == "View > Playlist"),
    ("Custom menu keeps open files", customMenuBindings.Any(binding => binding.Command == "script-message-to mpvnet open-files")),
    ("Custom menu keeps about", customMenuBindings.Any(binding => binding.Command == "script-message-to mpvnet show-about")),
    ("File log writer creates folder and daily log file", File.Exists(dailyLogFile)),
    ("File log writer writes Info", dailyLogContent.Contains("[INFO] info message")),
    ("File log writer writes Debug", dailyLogContent.Contains("[DEBUG] debug message")),
    ("File log writer writes Error exception", dailyLogContent.Contains("[ERROR] error message") && dailyLogContent.Contains("InvalidOperationException") && dailyLogContent.Contains("inner")),
    ("File log writer deletes logs older than three days", !File.Exists(Path.Combine(tempLogDir, "mpvnet-2026-05-29.log"))),
    ("File log writer keeps recent daily logs", File.Exists(Path.Combine(tempLogDir, "mpvnet-2026-05-30.log"))),
    ("File log writer ignores unrelated files during cleanup", File.Exists(Path.Combine(tempLogDir, "other-2026-05-01.log"))),
    ("File log writer does not throw on write failure", blockedWriteDidNotThrow),
    ("Log safe value masks URL query and fragment", safeUrlWithSecret == "https://example.com/live/index.m3u8?***#***"),
    ("Log safe value keeps plain URL unchanged", safePlainUrl == "https://example.com/live/index.m3u8"),
    ("Default log folder uses mpv.net LocalAppData root", Path.GetFullPath(Log.LogFolder).StartsWith(expectedLocalAppDataRoot, StringComparison.OrdinalIgnoreCase)),
    ("Default cache folder uses mpv.net LocalAppData root", Path.GetFullPath(defaultCacheFolder).StartsWith(expectedLocalAppDataRoot, StringComparison.OrdinalIgnoreCase)),
    ("Default cache folder is separate from logs", !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(defaultCacheFolder), Path.GetFullPath(Log.LogFolder))),
    ("Default temp folder uses mpv.net LocalAppData root", Path.GetFullPath(defaultTempFolder).StartsWith(expectedLocalAppDataRoot, StringComparison.OrdinalIgnoreCase)),
    ("Default temp folder is separate from cache", !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(defaultTempFolder), Path.GetFullPath(defaultCacheFolder))),
    ("Temporary cleanup deletes old cache files", !File.Exists(oldCacheFile)),
    ("Temporary cleanup deletes old temp files", !File.Exists(oldTempFile)),
    ("Temporary cleanup keeps recent cache files", File.Exists(recentCacheFile)),
    ("Temporary cleanup keeps recent temp files", File.Exists(recentTempFile)),
    ("Temporary cleanup deletes old empty directories", !Directory.Exists(oldEmptyDir)),
    ("Temporary cleanup ignores missing folders", missingCleanupFolderDidNotThrow),
    ("MediaInfo policy accepts enabled existing local file", MediaInfoPolicy.CanUseMediaInfo(true, tempMediaFile)),
    ("MediaInfo policy rejects disabled local file", !MediaInfoPolicy.CanUseMediaInfo(false, tempMediaFile)),
    ("MediaInfo policy rejects streaming URL", !MediaInfoPolicy.CanUseMediaInfo(true, "https://example.com/video.mp4")),
    ("MediaInfo policy rejects pipe path", !MediaInfoPolicy.CanUseMediaInfo(true, @"\\.\pipe\mpvnet-test")),
    ("MediaInfo policy rejects missing local file", !MediaInfoPolicy.CanUseMediaInfo(true, tempMediaFile + ".missing")),
    ("MPV track text helper trims and de-duplicates", mpvTrackText.Text == " AAC,"),
    ("MediaInfo track text helper trims and de-duplicates", mediaInfoTrackText.Text == " DTS,"),
    ("Native UTF-8 conversion accepts null pointer", LibMpv.ConvertFromUtf8(IntPtr.Zero) == ""),
    ("Native UTF-8 string array accepts null pointer", LibMpv.ConvertFromUtf8Strings(IntPtr.Zero, 0).Length == 0),
};

var duplicateTestNames = tests
    .GroupBy(test => test.Name)
    .Where(group => group.Count() > 1)
    .Select(group => group.Key)
    .ToArray();

if (duplicateTestNames.Length > 0)
    throw new Exception("Duplicate test names: " + string.Join(", ", duplicateTestNames));

var failed = tests.Where(test => !test.Result).ToArray();

File.Delete(tempMediaFile);
File.Delete(relativeMediaFile);
File.Delete(tempNormalizedM3u);
File.Delete(tempNormalizedPlsM3u);
File.Delete(tempRawTitleM3u);
Directory.Delete(tempPlaylistDir, true);
Directory.Delete(tempLogDir, true);
Directory.Delete(tempCleanupDir, true);

foreach (var test in tests)
    Console.WriteLine($"{(test.Result ? "PASS" : "FAIL")} {test.Name}");

if (failed.Length > 0)
    throw new Exception($"{failed.Length} media input support tests failed.");

sealed class TestTranslator : ITranslator
{
    public string Gettext(string msgId) => msgId;

    public string GetParticularString(string context, string text) => text;
}
