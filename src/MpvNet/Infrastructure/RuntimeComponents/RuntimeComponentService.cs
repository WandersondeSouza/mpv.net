using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MpvNet;

internal static class RuntimeComponentService
{
    static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(20);

    public static async Task EnsureComponentsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RuntimeComponentDefinition> definitions = RuntimeComponentCatalog.Definitions;
        Log.Info($"Starting runtime component bootstrap. folder='{Log.SafeValue(RuntimeComponentPaths.ComponentsFolder)}', count={definitions.Count}");
        Directory.CreateDirectory(RuntimeComponentPaths.ComponentsFolder);
        CleanupTempFolder();
        var stagedBundles = new Dictionary<string, StagedRuntimeComponent>(StringComparer.OrdinalIgnoreCase);

        foreach (RuntimeComponentDefinition definition in definitions)
        {
            try
            {
                await EnsureComponentAsync(definition, stagedBundles, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Component update failed for {definition.FileName}; continuing with the next component.");
            }
        }

        foreach (string path in stagedBundles.Values.Select(item => item.Path).Distinct(StringComparer.OrdinalIgnoreCase))
            RuntimeComponentFileSystem.DeleteIfExists(path);

        Log.Info("Runtime component bootstrap finished.");
    }

    static async Task EnsureComponentAsync(
        RuntimeComponentDefinition definition,
        Dictionary<string, StagedRuntimeComponent> stagedBundles,
        CancellationToken cancellationToken)
    {
        string targetPath = RuntimeComponentPaths.GetTargetPath(definition.FileName);
        string metadataPath = RuntimeComponentPaths.GetMetadataPath(definition);

        try
        {
            RuntimeComponentMetadata? metadata =
                await RuntimeComponentMetadataStore.LoadAsync(metadataPath, cancellationToken).ConfigureAwait(false);
            if (metadata is not null &&
                metadata.LastCheckedUtc > DateTimeOffset.UtcNow.Subtract(RefreshInterval) &&
                IsComponentReady(definition))
            {
                Log.Info($"Runtime component is fresh; skipping download. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
                return;
            }
        }
        catch
        {
            Log.Debug($"Failed to read component metadata; forcing refresh. file='{definition.FileName}', metadataPath='{Log.SafeValue(metadataPath)}'");
        }

        StagedRuntimeComponent staged = await DownloadComponentAsync(
            definition, stagedBundles, cancellationToken).ConfigureAwait(false);
        try
        {
            File.Copy(staged.Path, targetPath, overwrite: true);
            string digest = string.IsNullOrWhiteSpace(staged.Digest)
                ? RuntimeComponentFileSystem.GetFileDigest(targetPath)
                : staged.Digest;
            await RuntimeComponentMetadataStore.SaveAsync(metadataPath, digest!, cancellationToken).ConfigureAwait(false);
            Log.Info($"Runtime component updated successfully. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
        }
        catch (IOException ex) when (RuntimeComponentFileSystem.IsFileLocked(targetPath))
        {
            Log.Info($"Runtime component is in use; skipping update for now. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
            Log.Debug($"Runtime component update skipped because the target file is locked. file='{definition.FileName}', error='{Log.SafeValue(ex.Message)}'");
        }
        finally
        {
            RuntimeComponentFileSystem.DeleteIfExists(staged.Path);
        }
    }

    static async Task<StagedRuntimeComponent> DownloadComponentAsync(
        RuntimeComponentDefinition definition,
        Dictionary<string, StagedRuntimeComponent> stagedBundles,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RuntimeComponentPaths.TempFolder);
        if (definition.Kind == RuntimeComponentDownloadKind.GitHubZip)
            return await DownloadBundleComponentAsync(definition, stagedBundles, cancellationToken).ConfigureAwait(false);

        string downloadedPath = await DownloadReleaseAssetAsync(definition, cancellationToken).ConfigureAwait(false);
        string remoteDigest = await GetRemoteDigestAsync(definition, cancellationToken).ConfigureAwait(false);
        string localDigest = RuntimeComponentFileSystem.GetFileDigest(downloadedPath);
        ValidateDigest(definition.FileName, localDigest, remoteDigest, downloadedPath);
        return new StagedRuntimeComponent(downloadedPath,
            string.IsNullOrWhiteSpace(remoteDigest) ? localDigest : remoteDigest);
    }

    static async Task<StagedRuntimeComponent> DownloadBundleComponentAsync(
        RuntimeComponentDefinition definition,
        Dictionary<string, StagedRuntimeComponent> stagedBundles,
        CancellationToken cancellationToken)
    {
        string cacheKey = $"{definition.ReleaseApiUrl}|{definition.AssetPattern}|{definition.FileName}";
        if (stagedBundles.TryGetValue(cacheKey, out StagedRuntimeComponent? cached) && File.Exists(cached.Path))
            return cached;

        string archivePath = await DownloadReleaseAssetAsync(definition, cancellationToken).ConfigureAwait(false);
        string remoteDigest = await GetRemoteDigestAsync(definition, cancellationToken).ConfigureAwait(false);
        ValidateDigest(definition.FileName, RuntimeComponentFileSystem.GetFileDigest(archivePath), remoteDigest, archivePath);
        string stagedPath = ExtractRequiredAsset(archivePath, definition);
        var result = new StagedRuntimeComponent(stagedPath, remoteDigest);
        stagedBundles[cacheKey] = result;
        return result;
    }

    static async Task<string> DownloadReleaseAssetAsync(
        RuntimeComponentDefinition definition, CancellationToken cancellationToken)
    {
        GitHubRelease release = await GitHubReleaseClient.GetReleaseAsync(
            definition.ReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        GitHubAsset asset = release.Assets.FirstOrDefault(item =>
            Regex.IsMatch(item.Name ?? "", definition.AssetPattern, RegexOptions.IgnoreCase))
            ?? throw new InvalidOperationException($"Asset not found for {definition.FileName}.");
        string url = asset.BrowserDownloadUrl
            ?? throw new InvalidOperationException($"Missing download URL for {definition.FileName}.");
        string destination = Path.Combine(RuntimeComponentPaths.TempFolder, asset.Name ?? definition.FileName);
        Log.Info($"Downloading runtime component asset. file='{definition.FileName}', asset='{Log.SafeValue(asset.Name)}', kind={definition.Kind}, url='{Log.SafeValue(url)}'");
        await GitHubReleaseClient.DownloadAsync(url, destination, cancellationToken).ConfigureAwait(false);
        return destination;
    }

    static async Task<string> GetRemoteDigestAsync(
        RuntimeComponentDefinition definition, CancellationToken cancellationToken)
    {
        GitHubRelease release = await GitHubReleaseClient.GetReleaseAsync(
            definition.ReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        GitHubAsset? asset = release.Assets.FirstOrDefault(item =>
            Regex.IsMatch(item.Name ?? "", definition.AssetPattern, RegexOptions.IgnoreCase));
        return asset?.Digest?.Split(':', 2, StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
    }

    static string ExtractRequiredAsset(string archivePath, RuntimeComponentDefinition definition)
    {
        string extractDirectory = Path.Combine(
            RuntimeComponentPaths.TempFolder, Path.GetFileNameWithoutExtension(archivePath) + "-extract");
        try
        {
            RuntimeComponentFileSystem.DeleteIfExists(extractDirectory);
            Directory.CreateDirectory(extractDirectory);
            System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractDirectory);
            string requiredFile = definition.ExtractedFiles.Single();
            string sourcePath = Directory.GetFiles(
                extractDirectory, requiredFile, SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new InvalidOperationException($"Required extracted file not found: {requiredFile}");
            string stagedPath = Path.Combine(RuntimeComponentPaths.TempFolder, $"{definition.FileName}.extracted");
            File.Copy(sourcePath, stagedPath, overwrite: true);
            return stagedPath;
        }
        finally
        {
            RuntimeComponentFileSystem.DeleteIfExists(extractDirectory);
            RuntimeComponentFileSystem.DeleteIfExists(archivePath);
        }
    }

    static void ValidateDigest(string fileName, string localDigest, string remoteDigest, string downloadedPath)
    {
        if (!string.IsNullOrWhiteSpace(remoteDigest) &&
            !string.Equals(localDigest, remoteDigest, StringComparison.OrdinalIgnoreCase))
        {
            RuntimeComponentFileSystem.DeleteIfExists(downloadedPath);
            throw new InvalidOperationException($"Digest mismatch for {fileName}.");
        }
    }

    static bool IsComponentReady(RuntimeComponentDefinition definition)
    {
        if (definition.Kind != RuntimeComponentDownloadKind.GitHubZip)
            return File.Exists(RuntimeComponentPaths.GetTargetPath(definition.FileName));

        return RuntimeComponentCatalog.Definitions
            .Where(item => item.Kind == RuntimeComponentDownloadKind.GitHubZip &&
                           item.ReleaseApiUrl == definition.ReleaseApiUrl &&
                           item.AssetPattern == definition.AssetPattern)
            .All(item => File.Exists(RuntimeComponentPaths.GetTargetPath(item.FileName)));
    }

    static void CleanupTempFolder()
    {
        try
        {
            RuntimeComponentFileSystem.DeleteIfExists(RuntimeComponentPaths.TempFolder);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Failed to clean runtime component temp folder. folder='{Log.SafeValue(RuntimeComponentPaths.TempFolder)}'");
        }
    }
}
