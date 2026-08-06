using System.IO.Compression;
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
        Log.Debug(
            $"Starting runtime component bootstrap. localAppData='{Log.SafeValue(AppPaths.LocalAppData)}', components='{Log.SafeValue(RuntimeComponentPaths.ComponentsFolder)}', staging='{Log.SafeValue(RuntimeComponentPaths.TempFolder)}', count={definitions.Count}");
        Directory.CreateDirectory(RuntimeComponentPaths.ComponentsFolder);

        using RuntimeComponentUpdateLock updateLock = await RuntimeComponentUpdateLock.AcquireAsync(cancellationToken)
            .ConfigureAwait(false);
        RuntimeComponentStore.RecoverInterruptedPromotion();
        RuntimeComponentStore.CleanupStaleStaging();

        foreach (IGrouping<string, RuntimeComponentDefinition> bundle in definitions
                     .Where(item => item.Kind == RuntimeComponentDownloadKind.GitHubZip)
                     .GroupBy(item => $"{item.ReleaseApiUrl}|{item.AssetPattern}", StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EnsureBundleAsync(bundle.ToArray(), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FFmpeg bundle update failed; retaining the previous valid component generation.");
            }
        }

        foreach (RuntimeComponentDefinition definition in definitions.Where(
                     item => item.Kind != RuntimeComponentDownloadKind.GitHubZip))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await EnsureComponentAsync(definition, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Component update failed for {definition.FileName}; retaining the previous valid component generation.");
            }
        }

        Log.Debug("Runtime component bootstrap finished.");
    }

    static async Task EnsureBundleAsync(
        IReadOnlyList<RuntimeComponentDefinition> definitions,
        CancellationToken cancellationToken)
    {
        RuntimeComponentDefinition primary = definitions[0];
        RuntimeComponentMetadata? metadata = await LoadMetadataAsync(primary, cancellationToken).ConfigureAwait(false);
        if (IsFreshAndValid(metadata, definitions))
        {
            Log.Debug("FFmpeg bundle cache is fresh and valid; skipping update.");
            return;
        }

        string staging = RuntimeComponentStore.CreateStagingSnapshot();
        try
        {
            DownloadedRuntimeAsset archive = await DownloadReleaseAssetAsync(primary, staging, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                ValidateDigest(primary.FileName, RuntimeComponentFileSystem.GetFileDigest(archive.Path), archive.Digest);
                ExtractBundle(archive.Path, staging, definitions);
            }
            finally
            {
                RuntimeComponentFileSystem.DeleteIfExists(archive.Path);
            }

            ValidateComponents(staging, definitions);
            await SaveMetadataAsync(staging, primary, archive, definitions, cancellationToken).ConfigureAwait(false);
            await RuntimeComponentStore.PromoteAsync(staging, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RuntimeComponentFileSystem.DeleteIfExists(staging);
        }
    }

    static async Task EnsureComponentAsync(RuntimeComponentDefinition definition, CancellationToken cancellationToken)
    {
        RuntimeComponentMetadata? metadata = await LoadMetadataAsync(definition, cancellationToken).ConfigureAwait(false);
        if (IsFreshAndValid(metadata, [definition]))
        {
            Log.Debug($"Runtime component cache is fresh and valid; skipping update. file='{definition.FileName}'");
            return;
        }

        string staging = RuntimeComponentStore.CreateStagingSnapshot();
        try
        {
            DownloadedRuntimeAsset downloaded = await DownloadReleaseAssetAsync(definition, staging, cancellationToken)
                .ConfigureAwait(false);
            try
            {
                ValidateDigest(definition.FileName, RuntimeComponentFileSystem.GetFileDigest(downloaded.Path), downloaded.Digest);
                string target = Path.Combine(staging, definition.FileName);
                File.Move(downloaded.Path, target, overwrite: true);
            }
            finally
            {
                RuntimeComponentFileSystem.DeleteIfExists(downloaded.Path);
            }

            ValidateComponents(staging, [definition]);
            await SaveMetadataAsync(staging, definition, downloaded, [definition], cancellationToken).ConfigureAwait(false);
            await RuntimeComponentStore.PromoteAsync(staging, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            RuntimeComponentFileSystem.DeleteIfExists(staging);
        }
    }

    static async Task<RuntimeComponentMetadata?> LoadMetadataAsync(
        RuntimeComponentDefinition definition,
        CancellationToken cancellationToken)
    {
        string currentPath = RuntimeComponentPaths.GetMetadataPath(definition);
        if (File.Exists(currentPath))
            return await RuntimeComponentMetadataStore.LoadAsync(currentPath, cancellationToken).ConfigureAwait(false);

        string legacyPath = RuntimeComponentPaths.GetLegacyMetadataPath(definition);
        return File.Exists(legacyPath)
            ? await RuntimeComponentMetadataStore.LoadAsync(legacyPath, cancellationToken).ConfigureAwait(false)
            : null;
    }

    static bool IsFreshAndValid(
        RuntimeComponentMetadata? metadata,
        IReadOnlyList<RuntimeComponentDefinition> definitions)
    {
        if (metadata is null || metadata.LastCheckedUtc <= DateTimeOffset.UtcNow.Subtract(RefreshInterval) ||
            metadata.FileDigests is null || metadata.FileDigests.Count == 0)
        {
            return false;
        }

        try
        {
            foreach (RuntimeComponentDefinition definition in definitions)
            {
                string path = RuntimeComponentPaths.GetTargetPath(definition.FileName);
                if (!metadata.FileDigests.TryGetValue(definition.FileName, out string? expectedDigest) ||
                    !RuntimeComponentValidator.Validate(definition.FileName, path).IsValid ||
                    !string.Equals(RuntimeComponentFileSystem.GetFileDigest(path), expectedDigest, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug($"Cached runtime component is invalid or changed. file='{definition.FileName}', path='{Log.SafeValue(path)}'");
                    return false;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Debug($"Cached runtime component validation failed; forcing refresh. error='{Log.SafeValue(ex.Message)}'");
            return false;
        }

        return true;
    }

    static async Task<DownloadedRuntimeAsset> DownloadReleaseAssetAsync(
        RuntimeComponentDefinition definition,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        GitHubRelease release = await GitHubReleaseClient.GetReleaseAsync(
            definition.ReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        GitHubAsset asset = release.Assets.FirstOrDefault(item =>
            Regex.IsMatch(item.Name ?? "", definition.AssetPattern, RegexOptions.IgnoreCase))
            ?? throw new InvalidOperationException($"Asset not found for {definition.FileName}.");
        string assetName = asset.Name ?? throw new InvalidOperationException($"Missing asset name for {definition.FileName}.");
        if (!string.Equals(Path.GetFileName(assetName), assetName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
        {
            throw new InvalidOperationException($"Runtime component asset metadata is invalid for {definition.FileName}.");
        }

        string? digest = ParseSha256(asset.Digest) ?? ParseSha256(definition.PublishedDigest);
        if (digest is null)
            throw new InvalidOperationException($"A published SHA-256 digest is required for {definition.FileName}.");

        if (ParseSha256(asset.Digest) is null && definition.PublishedDigest is not null)
            Log.Debug($"Using the pinned SHA-256 for the legacy runtime component asset. file='{definition.FileName}', asset='{Log.SafeValue(assetName)}'");

        string destination = Path.Combine(stagingDirectory, assetName + "." + Guid.NewGuid().ToString("N") + ".download");
        Log.Debug($"Downloading runtime component asset. file='{definition.FileName}', asset='{Log.SafeValue(assetName)}', kind={definition.Kind}");
        long fileSize = await GitHubReleaseClient.DownloadAsync(
            asset.BrowserDownloadUrl!, destination, cancellationToken).ConfigureAwait(false);
        return new DownloadedRuntimeAsset(destination, digest, asset.BrowserDownloadUrl!, fileSize, assetName);
    }

    static string? ParseSha256(string? value)
    {
        string digest = value?.Split(':', 2, StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        return digest.Length == 64 && digest.All(Uri.IsHexDigit) ? digest : null;
    }

    internal static void ExtractBundle(
        string archivePath,
        string stagingDirectory,
        IReadOnlyList<RuntimeComponentDefinition> definitions)
    {
        string root = Path.GetFullPath(stagingDirectory) + Path.DirectorySeparatorChar;
        HashSet<string> required = definitions.Select(item => item.ExtractedFiles.Single())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string entryPath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(stagingDirectory, entryPath));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Runtime component archive contains a path outside of the staging directory.");

            if (entry.Name.Length == 0 || !required.Contains(entry.Name))
                continue;

            if (!extracted.Add(entry.Name))
                throw new InvalidOperationException($"Runtime component archive contains a duplicate file: {entry.Name}");

            string target = Path.Combine(stagingDirectory, entry.Name);
            RuntimeComponentFileSystem.DeleteIfExists(target);
            using Stream input = entry.Open();
            using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }

        foreach (string file in required)
        {
            if (!extracted.Contains(file))
                throw new InvalidOperationException($"Required extracted file not found: {file}");
        }
    }

    static void ValidateComponents(string directory, IReadOnlyList<RuntimeComponentDefinition> definitions)
    {
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RuntimeComponentDefinition definition in definitions)
        {
            string path = Path.Combine(directory, definition.FileName);
            ComponentValidationResult validation = RuntimeComponentValidator.Validate(definition.FileName, path);
            if (!validation.IsValid)
                throw new InvalidOperationException($"Runtime component validation failed for {definition.FileName}: {validation.DiagnosticMessage}");

            if (!string.IsNullOrWhiteSpace(validation.Version))
                versions.Add(validation.Version);
        }

        if (definitions.Count > 1 && versions.Count > 1)
            throw new InvalidOperationException("FFmpeg bundle executables have inconsistent version metadata.");
    }

    static async Task SaveMetadataAsync(
        string stagingDirectory,
        RuntimeComponentDefinition definition,
        DownloadedRuntimeAsset download,
        IReadOnlyList<RuntimeComponentDefinition> definitions,
        CancellationToken cancellationToken)
    {
        var digests = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (RuntimeComponentDefinition item in definitions)
        {
            string path = Path.Combine(stagingDirectory, item.FileName);
            digests[item.FileName] = RuntimeComponentFileSystem.GetFileDigest(path);
        }

        var metadata = new RuntimeComponentMetadata
        {
            Component = definition.Kind == RuntimeComponentDownloadKind.GitHubZip ? "ffmpeg-bundle" : definition.FileName,
            Version = download.AssetName,
            Digest = download.Digest,
            SourceUrl = download.SourceUrl,
            DownloadedAtUtc = DateTimeOffset.UtcNow,
            FileSize = download.FileSize,
            Architecture = "x64",
            FileDigests = digests
        };
        string metadataFileName = Path.GetFileName(RuntimeComponentPaths.GetMetadataPath(definition));
        await RuntimeComponentMetadataStore.SaveAsync(
            Path.Combine(stagingDirectory, metadataFileName), metadata, cancellationToken).ConfigureAwait(false);
    }

    static void ValidateDigest(string fileName, string localDigest, string remoteDigest)
    {
        if (!string.Equals(localDigest, remoteDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"SHA-256 digest mismatch for {fileName}.");

        Log.Debug($"Runtime component checksum validated. file='{fileName}', sha256='{localDigest[..12]}'");
    }
}
