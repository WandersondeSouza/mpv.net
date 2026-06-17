using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

public static class RuntimeComponents
{
    public static string ComponentsFolder { get; } = Path.Combine(Folder.LocalAppData, "mpv.net", "Component");
    static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(20);

    static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    static readonly IReadOnlyList<ComponentDefinition> Definitions =
    [
        new(
            "ffmpeg.exe",
            "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
            "^ffmpeg-(?:N-[0-9]+-g[0-9a-f]+|master-latest)-win64-gpl\\.zip$",
            ComponentDownloadKind.GitHubZip,
            "ffmpeg.exe"),
        new(
            "ffplay.exe",
            "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
            "^ffmpeg-(?:N-[0-9]+-g[0-9a-f]+|master-latest)-win64-gpl\\.zip$",
            ComponentDownloadKind.GitHubZip,
            "ffplay.exe"),
        new(
            "ffprobe.exe",
            "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest",
            "^ffmpeg-(?:N-[0-9]+-g[0-9a-f]+|master-latest)-win64-gpl\\.zip$",
            ComponentDownloadKind.GitHubZip,
            "ffprobe.exe"),
        new(
            "yt-dlp.exe",
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest",
            "^yt-dlp\\.exe$",
            ComponentDownloadKind.Direct),
        new(
            "mpvnet.com",
            "https://api.github.com/repos/mpvnet-player/file-host/releases/latest",
            "^mpvnet\\.com(?:\\.txt)?$",
            ComponentDownloadKind.Direct)
    ];

    public static void RegisterNativeResolver()
    {
        NativeLibrary.SetDllImportResolver(typeof(RuntimeComponents).Assembly, ResolveNativeLibrary);
    }

    public static async Task EnsureComponentsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ComponentsFolder);

        foreach (var component in Definitions)
        {
            await EnsureComponentAsync(component, cancellationToken).ConfigureAwait(false);
        }
    }

    public static string ResolveComponentPath(string fileName)
    {
        string componentPath = Path.Combine(ComponentsFolder, fileName);
        if (File.Exists(componentPath))
        {
            return componentPath;
        }

        string startupPath = Path.Combine(Folder.Startup, fileName);
        if (File.Exists(startupPath))
        {
            return startupPath;
        }

        return componentPath;
    }

    static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string? fileName = libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : libraryName + ".dll";

        string candidate = ResolveComponentPath(fileName);
        if (File.Exists(candidate))
        {
            return NativeLibrary.Load(candidate, assembly, searchPath);
        }

        return IntPtr.Zero;
    }

    static async Task EnsureComponentAsync(ComponentDefinition definition, CancellationToken cancellationToken)
    {
        string targetPath = Path.Combine(ComponentsFolder, definition.FileName);
        string metadataPath = targetPath + ".json";
        string? currentDigest = null;

        if (File.Exists(metadataPath))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<ComponentMetadata>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false), JsonOptions);
                currentDigest = metadata?.Digest;
                if (metadata is not null && metadata.LastCheckedUtc > DateTimeOffset.UtcNow.Subtract(RefreshInterval) && File.Exists(targetPath))
                {
                    return;
                }
            }
            catch
            {
                currentDigest = null;
            }
        }

        if (File.Exists(targetPath))
        {
            string remoteDigest = await GetRemoteDigestAsync(definition, cancellationToken).ConfigureAwait(false);
            if (string.Equals(currentDigest, remoteDigest, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetFileDigest(targetPath), remoteDigest, StringComparison.OrdinalIgnoreCase))
            {
                await SaveMetadataAsync(metadataPath, remoteDigest, cancellationToken).ConfigureAwait(false);
                return;
            }

            string downloadedPath = await DownloadLatestComponentAsync(definition, targetPath, cancellationToken).ConfigureAwait(false);
            await FinalizeComponentAsync(downloadedPath, targetPath, metadataPath, definition, cancellationToken).ConfigureAwait(false);
            return;
        }

        string packagedPath = Path.Combine(Folder.Startup, definition.FileName);
        if (File.Exists(packagedPath))
        {
            File.Copy(packagedPath, targetPath, overwrite: true);
            string digest = GetFileDigest(targetPath);
            await SaveMetadataAsync(metadataPath, digest, cancellationToken).ConfigureAwait(false);
#pragma warning disable CS4014
            Task.Run(async () =>
            {
                try
                {
                    string downloadedPath = await DownloadLatestComponentAsync(definition, targetPath, CancellationToken.None).ConfigureAwait(false);
                    await FinalizeComponentAsync(downloadedPath, targetPath, metadataPath, definition, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error($"Component refresh failed for {definition.FileName}: {ex.Message}");
                }
            });
#pragma warning restore CS4014
            return;
        }

        string downloaded = await DownloadLatestComponentAsync(definition, targetPath, cancellationToken).ConfigureAwait(false);
        await FinalizeComponentAsync(downloaded, targetPath, metadataPath, definition, cancellationToken).ConfigureAwait(false);
    }

    static async Task FinalizeComponentAsync(string downloadedPath, string targetPath, string metadataPath, ComponentDefinition definition, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (!string.Equals(downloadedPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(downloadedPath, targetPath, overwrite: true);
        }

        string digest = GetFileDigest(targetPath);
        string remoteDigest = await GetRemoteDigestAsync(definition, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(digest, remoteDigest, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Digest mismatch for {definition.FileName}.");
        }

        await SaveMetadataAsync(metadataPath, digest, cancellationToken).ConfigureAwait(false);
    }

    static async Task<string> DownloadLatestComponentAsync(ComponentDefinition definition, string targetPath, CancellationToken cancellationToken)
    {
        var release = await GetReleaseAsync(definition.ReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name ?? "", definition.AssetPattern, RegexOptions.IgnoreCase));
        if (asset is null)
        {
            throw new InvalidOperationException($"Asset not found for {definition.FileName}.");
        }

        string assetUrl = asset.BrowserDownloadUrl ?? throw new InvalidOperationException($"Missing download URL for {definition.FileName}.");
        string outputFile = definition.Kind == ComponentDownloadKind.GitHubZip
            ? Path.Combine(ComponentsFolder, asset.Name!)
            : targetPath;
        using var response = await Http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(outputFile);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        if (definition.Kind == ComponentDownloadKind.GitHubZip)
        {
            return ExtractRequiredGitHubZipAsset(outputFile, definition, targetPath);
        }

        return outputFile;
    }

    static string ExtractRequiredGitHubZipAsset(string zipFile, ComponentDefinition definition, string targetPath)
    {
        if (!File.Exists(zipFile))
        {
            throw new FileNotFoundException("Downloaded archive not found.", zipFile);
        }

        string extractDir = Path.Combine(ComponentsFolder, Path.GetFileNameWithoutExtension(zipFile) + "-extract");
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, true);
        }

        Directory.CreateDirectory(extractDir);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractDir);

        string required = definition.ExtractedFiles.Single();
        string? match = Directory.GetFiles(extractDir, required, SearchOption.AllDirectories).FirstOrDefault();
        if (match is null)
        {
            throw new InvalidOperationException($"Required extracted file not found: {required}");
        }

        File.Copy(match, targetPath, overwrite: true);
        return targetPath;
    }

    static async Task<string> GetRemoteDigestAsync(ComponentDefinition definition, CancellationToken cancellationToken)
    {
        var release = await GetReleaseAsync(definition.ReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name ?? "", definition.AssetPattern, RegexOptions.IgnoreCase));
        return asset?.Digest?.Split(':', 2, StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
    }

    static async Task<GithubRelease> GetReleaseAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("mpv.net", AppInfo.Version.ToString()));
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<GithubRelease>(json, JsonOptions) ?? throw new InvalidOperationException("Invalid GitHub release payload.");
    }

    static string GetFileDigest(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    static async Task SaveMetadataAsync(string metadataPath, string digest, CancellationToken cancellationToken)
    {
        var metadata = new ComponentMetadata
        {
            Digest = digest,
            LastCheckedUtc = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(metadataPath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    sealed record ComponentDefinition(string FileName, string ReleaseApiUrl, string AssetPattern, ComponentDownloadKind Kind, params string[] ExtractedFiles);

    enum ComponentDownloadKind
    {
        Direct,
        GitHubZip
    }

    sealed class GithubRelease
    {
        public GithubAsset[] Assets { get; set; } = [];
    }

    sealed class GithubAsset
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public string? Digest { get; set; }
    }

    sealed class ComponentMetadata
    {
        public string? Digest { get; set; }
        public DateTimeOffset LastCheckedUtc { get; set; }
    }
}
