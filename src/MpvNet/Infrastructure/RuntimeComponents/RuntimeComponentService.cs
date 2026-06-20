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

        foreach (IGrouping<string, RuntimeComponentDefinition> bundle in definitions
                     .Where(item => item.Kind == RuntimeComponentDownloadKind.GitHubZip)
                     .GroupBy(item => $"{item.ReleaseApiUrl}|{item.AssetPattern}", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await EnsureBundleAsync(bundle.ToArray(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FFmpeg bundle update failed; continuing with the next component.");
            }
        }

        foreach (RuntimeComponentDefinition definition in definitions.Where(
                     item => item.Kind != RuntimeComponentDownloadKind.GitHubZip))
        {
            try
            {
                await EnsureComponentAsync(definition, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Component update failed for {definition.FileName}; continuing with the next component.");
            }
        }

        Log.Info("Runtime component bootstrap finished.");
    }

    static async Task EnsureBundleAsync(
        IReadOnlyList<RuntimeComponentDefinition> definitions,
        CancellationToken cancellationToken)
    {
        RuntimeComponentDefinition primary = definitions[0];
        string metadataPath = RuntimeComponentPaths.GetMetadataPath(primary);

        try
        {
            RuntimeComponentMetadata? metadata =
                await RuntimeComponentMetadataStore.LoadAsync(metadataPath, cancellationToken).ConfigureAwait(false);
            if (metadata is not null &&
                metadata.LastCheckedUtc > DateTimeOffset.UtcNow.Subtract(RefreshInterval) &&
                definitions.All(item => File.Exists(RuntimeComponentPaths.GetTargetPath(item.FileName))))
            {
                Log.Info("FFmpeg bundle is fresh and complete; skipping download.");
                return;
            }
        }
        catch
        {
            Log.Debug($"Failed to read FFmpeg bundle metadata; forcing refresh. metadataPath='{Log.SafeValue(metadataPath)}'");
        }

        Directory.CreateDirectory(RuntimeComponentPaths.TempFolder);
        DownloadedRuntimeAsset archive =
            await DownloadReleaseAssetAsync(primary, cancellationToken).ConfigureAwait(false);
        string localDigest = RuntimeComponentFileSystem.GetFileDigest(archive.Path);
        ValidateDigest(primary.FileName, localDigest, archive.Digest, archive.Path);
        string digest = string.IsNullOrWhiteSpace(archive.Digest) ? localDigest : archive.Digest;
        string extractDirectory = ExtractBundle(archive.Path, definitions);

        try
        {
            foreach (RuntimeComponentDefinition definition in definitions)
            {
                string sourcePath = Path.Combine(extractDirectory, definition.FileName);
                string targetPath = RuntimeComponentPaths.GetTargetPath(definition.FileName);
                File.Copy(sourcePath, targetPath, overwrite: true);
                Log.Info($"Runtime bundle component updated successfully. file='{definition.FileName}', path='{Log.SafeValue(targetPath)}'");
            }

            await RuntimeComponentMetadataStore.SaveAsync(metadataPath, digest, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex) when (definitions.Any(item =>
                   RuntimeComponentFileSystem.IsFileLocked(RuntimeComponentPaths.GetTargetPath(item.FileName))))
        {
            Log.Info("One or more FFmpeg bundle files are in use; skipping bundle update for now.");
            Log.Debug($"FFmpeg bundle update skipped because a target file is locked. error='{Log.SafeValue(ex.Message)}'");
        }
        finally
        {
            RuntimeComponentFileSystem.DeleteIfExists(extractDirectory);
        }
    }

    static async Task EnsureComponentAsync(
        RuntimeComponentDefinition definition,
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
                File.Exists(targetPath))
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
            definition, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RuntimeComponentPaths.TempFolder);
        DownloadedRuntimeAsset downloaded =
            await DownloadReleaseAssetAsync(definition, cancellationToken).ConfigureAwait(false);
        string localDigest = RuntimeComponentFileSystem.GetFileDigest(downloaded.Path);
        ValidateDigest(definition.FileName, localDigest, downloaded.Digest, downloaded.Path);
        return new StagedRuntimeComponent(downloaded.Path,
            string.IsNullOrWhiteSpace(downloaded.Digest) ? localDigest : downloaded.Digest);
    }

    static async Task<DownloadedRuntimeAsset> DownloadReleaseAssetAsync(
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
        string digest = asset.Digest?.Split(':', 2, StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        Log.Info($"Downloading runtime component asset. file='{definition.FileName}', asset='{Log.SafeValue(asset.Name)}', kind={definition.Kind}, url='{Log.SafeValue(url)}'");
        await GitHubReleaseClient.DownloadAsync(url, destination, cancellationToken).ConfigureAwait(false);
        return new DownloadedRuntimeAsset(destination, digest);
    }

    static string ExtractBundle(
        string archivePath,
        IReadOnlyList<RuntimeComponentDefinition> definitions)
    {
        string extractDirectory = Path.Combine(
            RuntimeComponentPaths.TempFolder, Path.GetFileNameWithoutExtension(archivePath) + "-extract");
        try
        {
            RuntimeComponentFileSystem.DeleteIfExists(extractDirectory);
            Directory.CreateDirectory(extractDirectory);
            System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractDirectory);
            string stagedDirectory = Path.Combine(RuntimeComponentPaths.TempFolder, "ffmpeg-bundle");
            RuntimeComponentFileSystem.DeleteIfExists(stagedDirectory);
            Directory.CreateDirectory(stagedDirectory);

            foreach (RuntimeComponentDefinition definition in definitions)
            {
                string requiredFile = definition.ExtractedFiles.Single();
                string sourcePath = Directory.GetFiles(
                    extractDirectory, requiredFile, SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new InvalidOperationException($"Required extracted file not found: {requiredFile}");
                File.Copy(sourcePath, Path.Combine(stagedDirectory, definition.FileName), overwrite: true);
            }

            return stagedDirectory;
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
