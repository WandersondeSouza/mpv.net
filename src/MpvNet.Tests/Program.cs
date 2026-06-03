using System;
using System.IO;
using System.Linq;
using System.Text;

using MpvNet;
using MpvNet.Help;

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
File.WriteAllText(tempAudio, "");
File.WriteAllText(tempVideo, "");
File.WriteAllText(tempUnknown, "");
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

DateTime fixedLogDate = new(2026, 6, 2, 19, 45, 10, 123);
string tempLogDir = Path.Combine(Path.GetTempPath(), "mpvnet-log-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempLogDir);
File.WriteAllText(Path.Combine(tempLogDir, "mpvnet-2026-05-27.log"), "old");
File.WriteAllText(Path.Combine(tempLogDir, "mpvnet-2026-05-28.log"), "keep");
File.WriteAllText(Path.Combine(tempLogDir, "other-2026-05-01.log"), "unrelated");
var logWriter = new FileLogWriter(tempLogDir, () => fixedLogDate);
logWriter.Write(LogLevel.Info, "info message", null);
logWriter.Write(LogLevel.Debug, "debug message", null);
logWriter.Write(LogLevel.Error, "error message", new InvalidOperationException("outer", new Exception("inner")));
string dailyLogFile = Path.Combine(tempLogDir, "mpvnet-2026-06-02.log");
string dailyLogContent = File.ReadAllText(dailyLogFile);
string blockedLogPath = Path.Combine(tempLogDir, "blocked");
File.WriteAllText(blockedLogPath, "");
var blockedLogWriter = new FileLogWriter(blockedLogPath, () => fixedLogDate);
bool blockedWriteDidNotThrow = true;
string expectedLocalAppDataRoot = Path.Combine(Folder.LocalAppData, "mpv.net");
string defaultCacheFolder = new MainPlayer().CacheFolder;

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

var tests = new (string Name, bool Result)[]
{
    ("IsVideoFile .mp4", FileTypes.IsVideoFile(".mp4")),
    ("IsVideoFile .mkv", FileTypes.IsVideoFile(".mkv")),
    ("IsPlaylistFile .m3u8", FileTypes.IsPlaylistFile(".m3u8")),
    ("IsPlaylistFile .cue", FileTypes.IsPlaylistFile(".cue")),
    ("IsPlaylistFile .asx", FileTypes.IsPlaylistFile(".asx")),
    ("IsPlaylistFile .wpl", FileTypes.IsPlaylistFile(".wpl")),
    ("IsPlaylistFile .jspf", FileTypes.IsPlaylistFile(".jspf")),
    ("IsStreamingUrl https HLS", FileTypes.IsStreamingUrl("https://example.com/live.m3u8")),
    ("IsStreamingUrl rtmp", FileTypes.IsStreamingUrl("rtmp://server/live")),
    ("IsStreamingUrl rtsp", FileTypes.IsStreamingUrl("rtsp://server/stream")),
    ("IsStreamingUrl udp", FileTypes.IsStreamingUrl("udp://239.0.0.1:1234")),
    ("IsStreamingUrl is case-insensitive", FileTypes.IsStreamingUrl("HTTPS://example.com/live.m3u8")),
    ("URL query string supported", FileTypes.IsSupportedMediaInput("https://example.com/live/index.m3u8?token=abc123")),
    ("URL fragment supported", FileTypes.IsSupportedMediaInput("https://example.com/live/index.m3u8#stream")),
    ("Uppercase media extension supported", FileTypes.IsVideoFile("MOVIE.MKV")),
    ("Unknown file false", !FileTypes.IsSupportedMediaInput("example.unknown")),
    ("Empty text false", !FileTypes.IsSupportedMediaInput("")),
    ("URL does not depend on File.Exists", FileTypes.IsSupportedMediaInput("https://example.com/video.mp4")),
    ("Title normalization removes extension and dot separators", TitleHelp.NormalizeMediaTitle("filme.exemplo.2024.mkv") == "Filme Exemplo 2024"),
    ("Title normalization collapses repeated spaces", TitleHelp.NormalizeMediaTitle("  arquivo..com  ..pontos.mp4  ") == "Arquivo Com Pontos"),
    ("Title normalization replaces dot comma dash and underscore with spaces", TitleHelp.NormalizeMediaTitle("um,titulo-bem_trocado.mp4") == "Um Titulo Bem Trocado"),
    ("Title normalization removes configured characters", TitleHelp.NormalizeMediaTitle("@titulo#com$simbolos*.mp4") == "Titulocomsimbolos"),
    ("Title normalization uses default title when empty", TitleHelp.NormalizeMediaTitle("@#$*.mp4") == "Untitled Track"),
    ("Title normalization truncates long titles", TitleHelp.NormalizeMediaTitle(new string('a', 120) + ".mp4").Length == 100),
    ("Title normalization removes mpv.net suffix", TitleHelp.NormalizeMediaTitle("movie title - mpv.net") == "Movie Title"),
    ("Title normalization keeps unsupported extension text", TitleHelp.NormalizeMediaTitle("notes.backup") == "Notes Backup"),
    ("Command line accepts streaming URL", CommandLine.IsLoadableFileArgument("rtmps://example.com/live")),
    ("Command line accepts playlist file extension", CommandLine.IsLoadableFileArgument("iptv.m3u")),
    ("Command line accepts stdin pipe marker", CommandLine.IsLoadableFileArgument("-")),
    ("Command line rejects options as files", !CommandLine.IsLoadableFileArgument("--fullscreen")),
    ("Command line accepts absolute Windows path without existence check", CommandLine.IsLoadableFileArgument(@"C:\missing\movie.mkv")),
    ("Command line accepts relative dot path", CommandLine.IsLoadableFileArgument(@".\movie.mkv")),
    ("Local file can use optional MediaInfo when present", MainPlayer.CanUseMediaInfo(tempMediaFile)),
    ("Missing local file skips optional MediaInfo", !MainPlayer.CanUseMediaInfo(tempMediaFile + ".missing")),
    ("Streaming URL skips optional MediaInfo", !MainPlayer.CanUseMediaInfo("https://example.com/live/index.m3u8")),
    ("Pipe input skips optional MediaInfo", !MainPlayer.CanUseMediaInfo(@"\\.\pipe\mpvnet-test")),
    ("Streaming without duration is still loadable", CommandLine.IsLoadableFileArgument("https://example.com/live/no-duration")),
    ("Streaming without title is still loadable", CommandLine.IsLoadableFileArgument("rtsp://example.com/stream")),
    ("Remote M3U detection accepts UTF-8 BOM", MainPlayer.LooksLikeM3u(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("#EXTM3U")).ToArray())),
    ("Remote M3U detection accepts leading whitespace", MainPlayer.LooksLikeM3u(Encoding.UTF8.GetBytes("\r\n  #EXTM3U\r\n"))),
    ("Remote M3U detection rejects normal media", !MainPlayer.LooksLikeM3u(Encoding.UTF8.GetBytes("not a playlist"))),
    ("Invalid empty URL is not loadable", !CommandLine.IsLoadableFileArgument("")),
    ("Invalid unknown local path is not supported media input", !FileTypes.IsSupportedMediaInput(@"C:\missing\file.unknown")),
    ("Audio defaults keep legacy formats", legacyAudioExts.All(audioExts.Contains)),
    ("Audio defaults add modern formats", expectedAudioExts.All(audioExts.Contains)),
    ("Folder media filter includes playlists", FileTypes.GetMediaFiles([tempAudio, tempVideo, tempM3u, tempUnknown]).Count() == 3),
    ("Folder media filter keeps playlist files", FileTypes.GetMediaFiles([tempM3u]).Single() == tempM3u),
    ("Empty media track defaults avoid null bindings", new MediaTrack().Text == "" && new MediaTrack().Language == ""),
    ("Playlist parser keeps playable unique items", parsedPlaylist.Count == 2),
    ("Playlist parser resolves relative media paths", parsedPlaylist.Any(i => i.Path == tempAudio)),
    ("Playlist parser normalizes item title", parsedPlaylist.Any(i => i.Title == "Video Title" && i.Path == tempVideo)),
    ("Playlist writer preserves normalized item titles", normalizedM3uContent.Contains("#EXTINF:-1,Video Title")),
    ("Playlist writer preserves resolved paths", normalizedM3uContent.Contains(tempVideo)),
    ("Playlist normalizer keeps streaming URLs", normalizedRemotePlaylistItems.Single().Path == "https://example.com/live/index.m3u8?token=abc"),
    ("Playlist normalizer resolves file URIs", Path.GetFullPath(normalizedFileUriPlaylistItems.Single().Path) == Path.GetFullPath(tempAudio)),
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
    ("Config parser skips comments and invalid lines", parsedConfig.Count == 3),
    ("Config parser trims keys and values", parsedConfig["dark-mode"] == "never" && parsedConfig["language"] == "pt-BR"),
    ("Config parser keeps last duplicate value", parsedConfig["duplicate"] == "new"),
    ("Command parser ignores non-options and empty names", parsedCommandArguments.Count == 10),
    ("Command parser handles boolean flags", parsedCommandArguments.Any(i => i.Name == "terminal" && i.Value == "yes")),
    ("Command parser handles no-prefix flags", parsedCommandArguments.Any(i => i.Name == "config" && i.Value == "no")),
    ("Command parser normalizes aliases", parsedCommandArguments.Any(i => i.Name == "scripts" && i.Value == "test.lua") &&
        parsedCommandArguments.Any(i => i.Name == "script-opts" && i.Value == "thumbfast=yes") &&
        parsedCommandArguments.Any(i => i.Name == "audio-files" && i.Value == "audio.mp3") &&
        parsedCommandArguments.Any(i => i.Name == "sub-files" && i.Value == "sub.srt") &&
        parsedCommandArguments.Any(i => i.Name == "external-files" && i.Value == "cover.jpg")),
    ("Command parser normalizes explicit title values", parsedCommandArguments.Any(i => i.Name == "title" && i.Value == "Sample Video")),
    ("Command parser preserves title templates", parsedCommandArguments.Any(i => i.Name == "title" && i.Value == "${media-title}")),
    ("Command parser normalizes force media title", parsedCommandArguments.Any(i => i.Name == "force-media-title" && i.Value == "Forced Title")),
    ("File log writer creates folder and daily log file", File.Exists(dailyLogFile)),
    ("File log writer writes Info", dailyLogContent.Contains("[INFO] info message")),
    ("File log writer writes Debug", dailyLogContent.Contains("[DEBUG] debug message")),
    ("File log writer writes Error exception", dailyLogContent.Contains("[ERROR] error message") && dailyLogContent.Contains("InvalidOperationException") && dailyLogContent.Contains("inner")),
    ("File log writer deletes logs older than five days", !File.Exists(Path.Combine(tempLogDir, "mpvnet-2026-05-27.log"))),
    ("File log writer keeps recent daily logs", File.Exists(Path.Combine(tempLogDir, "mpvnet-2026-05-28.log"))),
    ("File log writer ignores unrelated files during cleanup", File.Exists(Path.Combine(tempLogDir, "other-2026-05-01.log"))),
    ("File log writer does not throw on write failure", blockedWriteDidNotThrow),
    ("Default log folder uses mpv.net LocalAppData root", Path.GetFullPath(Log.LogFolder).StartsWith(expectedLocalAppDataRoot, StringComparison.OrdinalIgnoreCase)),
    ("Default cache folder uses mpv.net LocalAppData root", Path.GetFullPath(defaultCacheFolder).StartsWith(expectedLocalAppDataRoot, StringComparison.OrdinalIgnoreCase)),
    ("Default cache folder is separate from logs", !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(defaultCacheFolder), Path.GetFullPath(Log.LogFolder))),
    ("MediaInfo policy accepts enabled existing local file", MediaInfoPolicy.CanUseMediaInfo(true, tempMediaFile)),
    ("MediaInfo policy rejects disabled local file", !MediaInfoPolicy.CanUseMediaInfo(false, tempMediaFile)),
    ("MediaInfo policy rejects streaming URL", !MediaInfoPolicy.CanUseMediaInfo(true, "https://example.com/video.mp4")),
    ("MediaInfo policy rejects pipe path", !MediaInfoPolicy.CanUseMediaInfo(true, @"\\.\pipe\mpvnet-test")),
    ("MediaInfo policy rejects missing local file", !MediaInfoPolicy.CanUseMediaInfo(true, tempMediaFile + ".missing")),
    ("MPV track text helper trims and de-duplicates", mpvTrackText.Text == " AAC,"),
    ("MediaInfo track text helper trims and de-duplicates", mediaInfoTrackText.Text == " DTS,"),
};

var failed = tests.Where(test => !test.Result).ToArray();

File.Delete(tempMediaFile);
File.Delete(tempNormalizedM3u);
File.Delete(tempNormalizedPlsM3u);
Directory.Delete(tempPlaylistDir, true);
Directory.Delete(tempLogDir, true);

foreach (var test in tests)
    Console.WriteLine($"{(test.Result ? "PASS" : "FAIL")} {test.Name}");

if (failed.Length > 0)
    throw new Exception($"{failed.Length} media input support tests failed.");
