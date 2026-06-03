namespace MpvNet;

internal static class MediaInfoPolicy
{
    public static bool CanUseMediaInfo(bool enabled, string path) =>
        enabled &&
        !string.IsNullOrWhiteSpace(path) &&
        !path.Contains("://") &&
        !path.Contains(@"\\.\pipe\") &&
        File.Exists(path);
}
