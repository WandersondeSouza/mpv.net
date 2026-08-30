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
}
