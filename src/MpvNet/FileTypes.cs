
using MpvNet.Extensions;

namespace MpvNet;

public static class FileTypes
{
    public static string[] Subtitle { get; } = ["srt", "ass", "idx", "sub", "sup", "ttxt", "txt", "ssa", "smi", "mks"];
    public static string[] Playlist { get; } = ["m3u", "m3u8", "pls", "xspf", "asx", "wpl", "cue", "jspf"];
    public static string[] StreamingProtocols { get; } = ["http://", "https://", "rtmp://", "rtmps://", "rtsp://", "mms://", "udp://", "tcp://", "ftp://", "sftp://"];
    public static string[] DefaultAudioExts { get; } = [
        "mp3", "wav", "flac", "m4a", "aac", "ogg", "opus", "wma",
        "alac", "aiff", "aif", "ape", "wv", "mka", "ac3", "dts",
        "eac3", "amr", "au", "mp2", "mpa", "mpc", "thd", "w64",
        "oga", "ogm", "dtshd", "dtshr", "dtsma"];
    public static string[] DefaultVideoExts { get; } = [
        "mp4", "m4v", "mkv", "webm", "avi", "mov", "qt", "wmv", "asf", "flv", "f4v",
        "mpg", "mpeg", "mpe", "m1v", "m2v", "vob", "ts", "mts", "m2ts", "3gp",
        "3g2", "ogv", "ogg", "rm", "rmvb", "divx", "xvid", "dv", "nut", "nsv",
        "264", "265", "avc", "avs", "dav", "h264", "h265", "hevc", "m2t", "mj2",
        "mpv", "vpy", "y4m"];
    public static string[] DefaultImageExts { get; } = [
        "avif", "bmp", "gif", "j2k", "jp2", "jpeg", "jpg", "jxl", "png",
        "svg", "tga", "tif", "tiff", "webp"];

    public static bool IsVideo(string[] exts, string ext) => exts?.Contains(ext) ?? false;
    public static bool IsAudio(string[] exts, string ext) => exts?.Contains(ext) ?? false;
    public static bool IsImage(string[] exts, string ext) => exts?.Contains(ext) ?? false;
    public static bool IsPlaylist(string ext) => Playlist.Contains(NormalizeExt(ext));
    public static bool IsStreamingUrl(string input) =>
        !string.IsNullOrWhiteSpace(input) &&
        StreamingProtocols.Any(protocol => input.StartsWith(protocol, StringComparison.OrdinalIgnoreCase));

    public static bool IsVideoFile(string input) => IsVideo(GetInputExtension(input));
    public static bool IsPlaylistFile(string input) => IsPlaylist(GetInputExtension(input));
    public static bool IsSupportedMediaInput(string input) =>
        IsStreamingUrl(input) || IsVideoFile(input) || IsPlaylistFile(input);

    public static bool IsVideo(string ext) => GetSupportedVideoExts().Contains(NormalizeExt(ext));
    public static bool IsAudio(string ext) => GetAudioExts().Contains(NormalizeExt(ext));
    public static bool IsImage(string ext) => GetImgExts().Contains(NormalizeExt(ext));

    public static string[] GetVideoExts()
    {
        string exts = Player.GetPropertyString("video-exts");

        if (string.IsNullOrEmpty(exts))
            return DefaultVideoExts;

        return SplitExtensions(exts).ToArray();
    }

    public static string[] GetSupportedVideoExts() => GetVideoExts().Union(DefaultVideoExts).ToArray();

    public static string[] GetAudioExts()
    {
        string exts = Player.GetPropertyString("audio-exts");

        if (string.IsNullOrEmpty(exts))
            return DefaultAudioExts;

        return SplitExtensions(exts).Distinct().ToArray();
    }

    public static string[] GetImgExts()
    {
        string exts = Player.GetPropertyString("image-exts");

        if (string.IsNullOrEmpty(exts))
            return DefaultImageExts;

        return SplitExtensions(exts).Distinct().ToArray();
    }

    public static bool IsMedia(string[] exts, string ext) =>
        IsVideo(exts, ext) || IsAudio(exts, ext) || IsImage(exts, ext);

    public static IEnumerable<string> GetMediaFiles(string[] files) =>
        files.Where(i => IsVideo(i.Ext()) || IsAudio(i.Ext()) || IsPlaylist(i.Ext()));

    public static IEnumerable<string> GetFolderMediaFiles(string[] files, string currentFile) =>
        IsImage(currentFile.Ext())
            ? files.Where(i => IsImage(i.Ext()))
            : GetMediaFiles(files);

    public static string GetOpenFileDialogFilter()
    {
        string video = string.Join(";", GetSupportedVideoExts().Select(ext => "*." + ext));
        string playlists = string.Join(";", Playlist.Select(ext => "*." + ext));
        return $"Video files|{video}|Playlists|{playlists}|All files (*.*)|*.*";
    }

    static IEnumerable<string> SplitExtensions(string exts) =>
        exts.Split(" ,;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(NormalizeExt);

    static string NormalizeExt(string ext) => ext.Trim().TrimStart('.').ToLowerInvariant();

    static string GetInputExtension(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        string value = input;
        int queryIndex = value.IndexOfAny(['?', '#']);

        if (queryIndex >= 0)
            value = value[..queryIndex];

        return NormalizeExt(Path.GetExtension(value));
    }
}
