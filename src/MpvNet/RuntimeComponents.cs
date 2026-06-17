using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

public static class RuntimeComponents
{
    public static string ComponentsFolder { get; } = Path.Combine(Folder.LocalAppData, "mpv.net", "Component");
    public static string TempFolder { get; } = Path.Combine(TemporaryFileCleanup.DefaultTempFolder, "RuntimeComponents");
    static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(20);
    static readonly TimeSpan ReleaseRequestTimeout = TimeSpan.FromSeconds(30);
    static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    static readonly HttpClient Http = new()
    {
        Timeout = DownloadTimeout
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
        Log.Info($"Starting runtime component bootstrap. folder='{Log.SafeValue(ComponentsFolder)}', count={Definitions.Count}");
        Directory.CreateDirectory(ComponentsFolder);
        CleanupTempFolder();
        Dictionary<string, string> stagedZipComponents = new(StringComparer.OrdinalIgnoreCase);

        foreach (var component in Definitions)
        {
            try
            {
                Log.Debug($"Ensuring runtime component: file='{component.FileName}', kind={component.Kind}, releaseApi='{Log.SafeValue(component.ReleaseApiUrl)}'");
                await EnsureComponentAsync(component, stagedZipComponents, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Component update failed for {component.FileName}; continuing with the next component.");
            }
        }

        CleanupStagedZipComponents(stagedZipComponents);

        Log.Info("Runtime component bootstrap finished.");
    }

    static void CleanupStagedZipComponents(Dictionary<string, string> stagedZipComponents)
    {
        foreach (string stagedPath in stagedZipComponents.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            DeleteIfExists(stagedPath);
        }
    }

    static void CleanupTempFolder()
    {
        try
        {
            if (Directory.Exists(TempFolder))
            {
                Directory.Delete(TempFolder, true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to clean runtime component temp folder. folder='{Log.SafeValue(TempFolder)}'");
        }
    }

    public static string ResolveComponentPath(string fileName)
    {
        string componentPath = Path.Combine(ComponentsFolder, fileName);
        if (File.Exists(componentPath))
        {
            Log.Info($"Resolved runtime component from component folder. file='{fileName}', path='{Log.SafeValue(componentPath)}'");
            return componentPath;
        }

        string startupPath = Path.Combine(Folder.Startup, fileName);
        if (File.Exists(startupPath))
        {
            Log.Info($"Resolved runtime component from startup folder. file='{fileName}', path='{Log.SafeValue(startupPath)}'");
            return startupPath;
        }

        string? pathCandidate = ResolveFromWindowsPath(fileName);
        if (!string.IsNullOrWhiteSpace(pathCandidate))
        {
            Log.Info($"Resolved runtime component from PATH. file='{fileName}', path='{Log.SafeValue(pathCandidate)}'");
        }

        Log.Debug($"Resolved runtime component fallback. file='{fileName}', componentPath='{Log.SafeValue(componentPath)}', startupPath='{Log.SafeValue(startupPath)}', pathCandidate='{Log.SafeValue(pathCandidate)}'");
        return pathCandidate ?? componentPath;
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

    static async Task EnsureComponentAsync(ComponentDefinition definition, Dictionary<string, string> stagedZipComponents, CancellationToken cancellationToken)
    {
        string targetPath = Path.Combine(ComponentsFolder, definition.FileName);
        string metadataPath = targetPath + ".json";
        string? currentDigest = null;

        Log.Debug(
            $"Runtime component resolution snapshot. file='{definition.FileName}', " +
            $"componentExists={File.Exists(targetPath)}, " +
            $"startupExists={File.Exists(Path.Combine(Folder.Startup, definition.FileName))}, " +
            $"pathExists={ResolveFromWindowsPath(definition.FileName) is not null}, " +
            $"targetPath='{Log.SafeValue(targetPath)}', startupPath='{Log.SafeValue(Path.Combine(Folder.Startup, definition.FileName))}'");

        if (File.Exists(metadataPath))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<ComponentMetadata>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false), JsonOptions);
                currentDigest = metadata?.Digest;
                Log.Debug($"Loaded component metadata: file='{definition.FileName}', digest='{currentDigest}', lastCheckedUtc={metadata?.LastCheckedUtc:O}");
                if (metadata is not null && metadata.LastCheckedUtc > DateTimeOffset.UtcNow.Subtract(RefreshInterval) && File.Exists(targetPath))
                {
                    Log.Info($"Runtime component is fresh; skipping download. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
                    return;
                }
            }
            catch
            {
                Log.Debug($"Failed to read component metadata; forcing refresh. file='{definition.FileName}', metadataPath='{Log.SafeValue(metadataPath)}'");
                currentDigest = null;
            }
        }
        else
        {
            Log.Debug($"No component metadata found; forcing refresh. file='{definition.FileName}', metadataPath='{Log.SafeValue(metadataPath)}'");
        }

        string downloaded = await DownloadComponentAsync(definition, stagedZipComponents, cancellationToken).ConfigureAwait(false);
        try
        {
            await CopyAndValidateComponentAsync(downloaded, targetPath, metadataPath, definition, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            if (IsFileLocked(targetPath))
            {
                Log.Info($"Runtime component is in use; skipping update for now. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
                Log.Debug($"Runtime component update skipped because the target file is locked. file='{definition.FileName}'");
            }
            else
            {
                Log.Error(ex, $"Component update failed for {definition.FileName}; continuing with the next component.");
            }
        }
        finally
        {
            DeleteIfExists(downloaded);
        }
    }

    static string? ResolveFromWindowsPath(string fileName)
    {
        string? windowsPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(windowsPath))
        {
            return null;
        }

        foreach (string rawDir in windowsPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string dir = rawDir.Trim();
            if (dir.Length == 0)
            {
                continue;
            }

            string candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    static async Task CopyAndValidateComponentAsync(string stagedPath, string targetPath, string metadataPath, ComponentDefinition definition, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Log.Debug($"Copying staged runtime component into place. file='{definition.FileName}', staged='{Log.SafeValue(stagedPath)}', target='{Log.SafeValue(targetPath)}'");
        File.Copy(stagedPath, targetPath, overwrite: true);
        string digest = GetFileDigest(targetPath);
        string remoteDigest = await GetRemoteDigestAsync(definition, cancellationToken).ConfigureAwait(false);
        Log.Debug($"Validated component digest. file='{definition.FileName}', localDigest='{digest}', remoteDigest='{remoteDigest}'");

        if (string.IsNullOrWhiteSpace(remoteDigest))
        {
            Log.Info($"Runtime component updated without remote digest validation. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
            await SaveMetadataAsync(metadataPath, digest, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(digest, remoteDigest, StringComparison.OrdinalIgnoreCase))
        {
            DeleteIfExists(targetPath);
            throw new InvalidOperationException($"Digest mismatch for {definition.FileName}.");
        }

        await SaveMetadataAsync(metadataPath, digest, cancellationToken).ConfigureAwait(false);
        Log.Info($"Runtime component updated successfully. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
    }

    static async Task<string> DownloadComponentAsync(ComponentDefinition definition, Dictionary<string, string> stagedZipComponents, CancellationToken cancellationToken)
    {
        if (definition.Kind == ComponentDownloadKind.GitHubZip)
        {
            return await DownloadSharedZipComponentAsync(definition, stagedZipComponents, cancellationToken).ConfigureAwait(false);
        }

        return await DownloadDirectComponentAsync(definition, cancellationToken).ConfigureAwait(false);
    }

    static async Task<string> DownloadSharedZipComponentAsync(ComponentDefinition definition, Dictionary<string, string> stagedZipComponents, CancellationToken cancellationToken)
    {
        string zipCacheKey = $"{definition.ReleaseApiUrl}|{definition.AssetPattern}";
        if (definition.Kind == ComponentDownloadKind.GitHubZip && stagedZipComponents.TryGetValue(zipCacheKey, out string? cachedPath) && File.Exists(cachedPath))
        {
            Log.Debug($"Reusing staged runtime component from shared FFmpeg zip cache. file='{definition.FileName}', cachedPath='{Log.SafeValue(cachedPath)}'");
            return cachedPath;
        }

        Directory.CreateDirectory(TempFolder);
        string downloadedZipPath = await DownloadComponentArchiveAsync(definition, cancellationToken).ConfigureAwait(false);
        string stagedPath = ExtractRequiredGitHubZipAsset(downloadedZipPath, definition);
        stagedZipComponents[zipCacheKey] = stagedPath;
        return stagedPath;
    }

    static async Task<string> DownloadDirectComponentAsync(ComponentDefinition definition, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(TempFolder);
        string downloadedPath = await DownloadReleaseAssetAsync(definition, cancellationToken).ConfigureAwait(false);
        Log.Debug($"Downloaded direct runtime component to temp path. file='{definition.FileName}', path='{Log.SafeValue(downloadedPath)}'");
        return downloadedPath;
    }

    static async Task<string> DownloadComponentArchiveAsync(ComponentDefinition definition, CancellationToken cancellationToken)
    {
        string downloadedPath = await DownloadReleaseAssetAsync(definition, cancellationToken).ConfigureAwait(false);
        Log.Debug($"Downloaded runtime component archive to temp path. file='{definition.FileName}', path='{Log.SafeValue(downloadedPath)}'");
        return downloadedPath;
    }

    static async Task<string> DownloadReleaseAssetAsync(ComponentDefinition definition, CancellationToken cancellationToken)
    {
        var release = await GetReleaseAsync(definition.ReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        var asset = release.Assets.FirstOrDefault(a => Regex.IsMatch(a.Name ?? "", definition.AssetPattern, RegexOptions.IgnoreCase));
        if (asset is null)
        {
            throw new InvalidOperationException($"Asset not found for {definition.FileName}.");
        }

        string assetUrl = asset.BrowserDownloadUrl ?? throw new InvalidOperationException($"Missing download URL for {definition.FileName}.");
        Log.Info($"Downloading runtime component. file='{definition.FileName}', asset='{Log.SafeValue(asset.Name)}', kind={definition.Kind}, url='{Log.SafeValue(assetUrl)}'");
        string tempDownloadPath = Path.Combine(TempFolder, asset.Name ?? definition.FileName);
        Log.Debug($"Downloading runtime component into temp file. file='{definition.FileName}', tempPath='{Log.SafeValue(tempDownloadPath)}'");
        try
        {
            using var response = await Http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = File.Create(tempDownloadPath);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DeleteIfExists(tempDownloadPath);
            throw;
        }

        return tempDownloadPath;
    }

    static string ExtractRequiredGitHubZipAsset(string zipFile, ComponentDefinition definition)
    {
        if (!File.Exists(zipFile))
        {
            throw new FileNotFoundException("Downloaded archive not found.", zipFile);
        }

        string extractDir = Path.Combine(TempFolder, Path.GetFileNameWithoutExtension(zipFile) + "-extract");
        try
        {
            if (Directory.Exists(extractDir))
            {
                Log.Debug($"Removing stale extraction directory. dir='{Log.SafeValue(extractDir)}'");
                Directory.Delete(extractDir, true);
            }

            Log.Info($"Extracting runtime component archive. file='{definition.FileName}', zip='{Log.SafeValue(zipFile)}', extractDir='{Log.SafeValue(extractDir)}'");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, extractDir);

            string required = definition.ExtractedFiles.Single();
            string? match = Directory.GetFiles(extractDir, required, SearchOption.AllDirectories).FirstOrDefault();
            if (match is null)
            {
                throw new InvalidOperationException($"Required extracted file not found: {required}");
            }

            string extractedPath = Path.Combine(TempFolder, $"{definition.FileName}.extracted");
            Log.Debug($"Copying extracted runtime component to staged temp path. file='{definition.FileName}', extracted='{Log.SafeValue(match)}', tempPath='{Log.SafeValue(extractedPath)}'");
            File.Copy(match, extractedPath, overwrite: true);
            return extractedPath;
        }
        finally
        {
            DeleteIfExists(extractDir);
        }
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
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ReleaseRequestTimeout);
        try
        {
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GithubRelease>(json, JsonOptions) ?? throw new InvalidOperationException("Invalid GitHub release payload.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            Log.Error($"Timed out while reading GitHub release metadata after {ReleaseRequestTimeout.TotalSeconds:0}s. url='{Log.SafeValue(url)}'");
            throw;
        }
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

    static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to delete temporary runtime component path. path='{Log.SafeValue(path)}'");
        }
    }

    static bool IsFileLocked(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
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
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }

    sealed class ComponentMetadata
    {
        public string? Digest { get; set; }
        public DateTimeOffset LastCheckedUtc { get; set; }
    }
}
