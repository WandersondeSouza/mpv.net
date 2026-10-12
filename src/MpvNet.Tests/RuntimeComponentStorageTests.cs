using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MpvNet;

using Xunit;

namespace MpvNet.Tests;

public sealed class RuntimeComponentStorageTests
{
    [Fact]
    public async Task InvalidMetadataReturnsNullWithoutDeletingTheFile()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "component.json");
        File.WriteAllText(path, "{ invalid metadata");

        RuntimeComponentMetadata? metadata = await RuntimeComponentMetadataStore.LoadAsync(
            path, CancellationToken.None);

        Assert.Null(metadata);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void InvalidComponentPathDoesNotEscapeTheManagedCache()
    {
        string path = RuntimeComponents.ResolveComponentPath("../outside.exe");

        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void StagingRetentionUsesOneDayCutoff()
    {
        DateTimeOffset now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

        Assert.True(RuntimeComponentStore.IsStaleStaging(now - RuntimeComponentStore.StagingRetention - TimeSpan.FromMinutes(1), now));
        Assert.False(RuntimeComponentStore.IsStaleStaging(now - RuntimeComponentStore.StagingRetention + TimeSpan.FromMinutes(1), now));
    }

    [Fact]
    public void StagingCleanupRemovesOnlyOldGuidDirectories()
    {
        using TestDirectory directory = new();
        DateTimeOffset now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        string stale = Path.Combine(directory.Path, Guid.NewGuid().ToString("N"));
        string recent = Path.Combine(directory.Path, Guid.NewGuid().ToString("N"));
        string unrelated = Path.Combine(directory.Path, "keep-me");

        Directory.CreateDirectory(Path.Combine(stale, "nested"));
        Directory.CreateDirectory(recent);
        Directory.CreateDirectory(unrelated);
        Directory.SetLastWriteTimeUtc(
            stale, (now - RuntimeComponentStore.StagingRetention - TimeSpan.FromMinutes(1)).UtcDateTime);
        Directory.SetLastWriteTimeUtc(
            recent, (now - RuntimeComponentStore.StagingRetention + TimeSpan.FromMinutes(1)).UtcDateTime);

        int removed = RuntimeComponentStore.CleanupStaleStaging(directory.Path, now);

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(recent));
        Assert.True(Directory.Exists(unrelated));
    }

    [Fact]
    public async Task UnchangedReleaseValidatesLocalBytesAndPreservesTheDownloadDate()
    {
        using TestDirectory directory = new();
        RuntimeComponentDefinition definition = new("yt-dlp.exe", "", "", RuntimeComponentDownloadKind.Direct);
        RuntimeComponentMetadata metadata = CreateMetadata(directory.Path, definition);
        DateTimeOffset downloadDate = metadata.LastCheckedUtc;

        bool reused = await RuntimeComponentService.TryReuseUnchangedAsync(
            new GitHubAsset { Digest = "sha256:" + metadata.Digest }, metadata, [definition],
            CancellationToken.None, directory.Path);
        RuntimeComponentMetadata? saved = await RuntimeComponentMetadataStore.LoadAsync(
            Path.Combine(directory.Path, "yt-dlp.exe.json"), CancellationToken.None);

        Assert.True(reused);
        Assert.Equal(downloadDate, saved!.LastCheckedUtc);
        Assert.True(saved.LastReleaseCheckUtc > downloadDate);
        Assert.True(await RuntimeComponentService.IsFreshAndValidAsync(
            saved, [definition], CancellationToken.None, componentFolder: directory.Path));
    }

    [Fact]
    public async Task EqualFingerprintDoesNotHideChangedBytesWhenRecheckingARelease()
    {
        using TestDirectory directory = new();
        RuntimeComponentDefinition definition = new("yt-dlp.exe", "", "", RuntimeComponentDownloadKind.Direct);
        RuntimeComponentMetadata metadata = CreateMetadata(directory.Path, definition);
        string path = Path.Combine(directory.Path, definition.FileName);
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        byte[] content = File.ReadAllBytes(path);
        content[^1] ^= 1;
        File.WriteAllBytes(path, content);
        File.SetLastWriteTimeUtc(path, timestamp);
        Assert.Equal(metadata.FileFingerprints![definition.FileName], RuntimeComponentFileSystem.GetFileFingerprint(path));

        bool reused = await RuntimeComponentService.TryReuseUnchangedAsync(
            new GitHubAsset { Digest = metadata.Digest }, metadata, [definition],
            CancellationToken.None, directory.Path);

        Assert.False(reused);
        Assert.Null(metadata.LastReleaseCheckUtc);
    }

    [Fact]
    public async Task ChangedPublishedDigestRequiresADownload()
    {
        using TestDirectory directory = new();
        RuntimeComponentDefinition definition = new("yt-dlp.exe", "", "", RuntimeComponentDownloadKind.Direct);
        RuntimeComponentMetadata metadata = CreateMetadata(directory.Path, definition);

        Assert.False(await RuntimeComponentService.TryReuseUnchangedAsync(
            new GitHubAsset { Digest = new string('a', 64) }, metadata, [definition],
            CancellationToken.None, directory.Path));
        Assert.Null(metadata.LastReleaseCheckUtc);
    }

    [Fact]
    public async Task UnchangedBundleWithAMissingExecutableStillRequiresADownload()
    {
        using TestDirectory directory = new();
        RuntimeComponentDefinition first = new("ffmpeg.exe", "", "", RuntimeComponentDownloadKind.GitHubZip, "ffmpeg.exe");
        RuntimeComponentDefinition missing = new("ffprobe.exe", "", "", RuntimeComponentDownloadKind.GitHubZip, "ffprobe.exe");
        RuntimeComponentMetadata metadata = CreateMetadata(directory.Path, first);

        Assert.False(await RuntimeComponentService.TryReuseUnchangedAsync(
            new GitHubAsset { Digest = metadata.Digest }, metadata, [first, missing],
            CancellationToken.None, directory.Path));
        Assert.Null(metadata.LastReleaseCheckUtc);
    }

    [Fact]
    public async Task OldMetadataStillUsesTheDownloadTimestampUntilTheReleaseIsRechecked()
    {
        using TestDirectory directory = new();
        RuntimeComponentDefinition definition = new("yt-dlp.exe", "", "", RuntimeComponentDownloadKind.Direct);
        RuntimeComponentMetadata metadata = CreateMetadata(directory.Path, definition);

        Assert.Equal(metadata.LastCheckedUtc, RuntimeComponentService.GetLastFreshnessCheck(metadata));
        Assert.False(await RuntimeComponentService.IsFreshAndValidAsync(
            metadata, [definition], CancellationToken.None, componentFolder: directory.Path));
    }

    [Fact]
    public void PartialIdentityChangesWithTheAssetUrlOrPublishedDigest()
    {
        string original = RuntimeComponentDownloads.GetPartialFileName("https://github.com/tool", new string('a', 64));

        Assert.Equal(original, RuntimeComponentDownloads.GetPartialFileName("https://github.com/tool", new string('A', 64)));
        Assert.NotEqual(original, RuntimeComponentDownloads.GetPartialFileName("https://github.com/tool", new string('b', 64)));
        Assert.NotEqual(original, RuntimeComponentDownloads.GetPartialFileName("https://github.com/other", new string('a', 64)));
        Assert.Equal(72, original.Length);
    }

    [Fact]
    public void PartialCleanupPreservesRecentTransfersAndUnrelatedFiles()
    {
        using TestDirectory directory = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string old = Path.Combine(directory.Path, new string('a', 64) + ".partial");
        string recent = Path.Combine(directory.Path, new string('b', 64) + ".partial");
        string unrelated = Path.Combine(directory.Path, "keep.partial");
        foreach (string path in new[] { old, recent, unrelated })
            File.WriteAllText(path, "partial");
        File.SetLastWriteTimeUtc(old, (now - TimeSpan.FromDays(8)).UtcDateTime);
        File.SetLastWriteTimeUtc(unrelated, (now - TimeSpan.FromDays(8)).UtcDateTime);

        RuntimeComponentDownloads.CleanupStalePartials(directory.Path, now);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(unrelated));
    }

    static RuntimeComponentMetadata CreateMetadata(string directory, RuntimeComponentDefinition definition)
    {
        string path = Path.Combine(directory, definition.FileName);
        byte[] content = new byte[256];
        content[0] = (byte)'M';
        content[1] = (byte)'Z';
        BitConverter.GetBytes(64).CopyTo(content, 60);
        content[64] = (byte)'P';
        content[65] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(content, 68);
        File.WriteAllBytes(path, content);
        (string digest, RuntimeComponentFileFingerprint fingerprint) = RuntimeComponentFileSystem.GetFileDigestAndFingerprint(path);
        return new RuntimeComponentMetadata
        {
            Digest = digest,
            LastCheckedUtc = DateTimeOffset.UtcNow - TimeSpan.FromDays(21),
            FileDigests = new Dictionary<string, string> { [definition.FileName] = digest },
            FileFingerprints = new Dictionary<string, RuntimeComponentFileFingerprint> { [definition.FileName] = fingerprint }
        };
    }
}
