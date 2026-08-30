using System;
using System.IO;
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
}
