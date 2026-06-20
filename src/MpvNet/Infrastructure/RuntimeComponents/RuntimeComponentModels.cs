using System.Text.Json.Serialization;

namespace MpvNet;

internal enum RuntimeComponentDownloadKind
{
    Direct,
    GitHubZip
}

internal sealed record RuntimeComponentDefinition(
    string FileName,
    string ReleaseApiUrl,
    string AssetPattern,
    RuntimeComponentDownloadKind Kind,
    params string[] ExtractedFiles);

internal sealed record StagedRuntimeComponent(string Path, string? Digest);

internal sealed record DownloadedRuntimeAsset(string Path, string Digest);

internal sealed class GitHubRelease
{
    public GitHubAsset[] Assets { get; set; } = [];
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("digest")]
    public string? Digest { get; set; }
}

internal sealed class RuntimeComponentMetadata
{
    public string? Digest { get; set; }
    public DateTimeOffset LastCheckedUtc { get; set; }
}
