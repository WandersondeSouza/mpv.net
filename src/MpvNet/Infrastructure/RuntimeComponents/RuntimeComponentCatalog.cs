namespace MpvNet;

internal static class RuntimeComponentCatalog
{
    public static IReadOnlyList<RuntimeComponentDefinition> Definitions { get; } =
    [
        new("ffmpeg.exe", "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
            "^ffmpeg-(?:N-[0-9]+-g[0-9a-f]+|master-latest)-win64-gpl\\.zip$",
            RuntimeComponentDownloadKind.GitHubZip, "ffmpeg.exe"),
        new("ffplay.exe", "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
            "^ffmpeg-(?:N-[0-9]+-g[0-9a-f]+|master-latest)-win64-gpl\\.zip$",
            RuntimeComponentDownloadKind.GitHubZip, "ffplay.exe"),
        new("ffprobe.exe", "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
            "^ffmpeg-(?:N-[0-9]+-g[0-9a-f]+|master-latest)-win64-gpl\\.zip$",
            RuntimeComponentDownloadKind.GitHubZip, "ffprobe.exe"),
        new("yt-dlp.exe", "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest",
            "^yt-dlp\\.exe$", RuntimeComponentDownloadKind.Direct),
        new("mpvnet.com", "https://api.github.com/repos/mpvnet-player/file-host/releases/latest",
            "^mpvnet\\.com(?:\\.txt)?$", RuntimeComponentDownloadKind.Direct)
    ];
}
