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
}
