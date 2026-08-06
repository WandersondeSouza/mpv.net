using System.Text.Json.Serialization;

namespace MpvNet;

internal enum RuntimeComponentDownloadKind
{
    Direct,
    GitHubZip
}

public enum ComponentSource
{
    None,
    ApplicationDirectory,
    ComponentCache,
    EnvironmentPath
}

/// <summary>
/// Describes the file selected for an optional executable component without
/// relying on the process working directory.
/// </summary>
public sealed record ComponentResolutionResult(
    string ComponentName,
    string? ResolvedPath,
    ComponentSource Source,
    bool Exists,
    bool IsValid,
    string? Version,
    string? Sha256,
    string? DiagnosticMessage);

internal sealed record ComponentValidationResult(
    bool IsValid,
    string? Version,
    string? DiagnosticMessage);

internal sealed record RuntimeComponentDefinition(
    string FileName,
    string ReleaseApiUrl,
    string AssetPattern,
    RuntimeComponentDownloadKind Kind,
    params string[] ExtractedFiles);

internal sealed record StagedRuntimeComponent(string Path, string? Digest);

internal sealed record DownloadedRuntimeAsset(string Path, string Digest, string SourceUrl, long FileSize, string AssetName);

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
    public string? Component { get; set; }
    public string? Version { get; set; }
    public string? Digest { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset DownloadedAtUtc { get; set; }
    public DateTimeOffset LastCheckedUtc { get; set; }
    public long FileSize { get; set; }
    public string? Architecture { get; set; }
    public Dictionary<string, string>? FileDigests { get; set; }
}
