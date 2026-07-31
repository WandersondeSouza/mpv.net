using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MpvNet;
using MpvNet.Help;
using Xunit;

namespace MpvNet.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTripPreservesSettings()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "settings.xml");
        AppSettings expected = new()
        {
            InputDefaultBindingsFixApplied = true,
            ShowMenuFixApplied = true,
            MenuUpdateVersion = 7,
            Volume = 42,
            AudioDevice = "default",
            ConfigEditorSearch = "Audio:",
            Mute = "yes",
            StartupFolder = directory.Path,
            RecentFiles = ["one.mkv", "two.m3u8"],
            WindowLocation = new(10, 20),
            WindowPosition = new(30, 40),
            WindowSize = new(1280, 720),
        };

        SettingsStore.Save(path, expected);
        AppSettings actual = SettingsStore.Load(path);

        Assert.Equal(expected.InputDefaultBindingsFixApplied, actual.InputDefaultBindingsFixApplied);
        Assert.Equal(expected.ShowMenuFixApplied, actual.ShowMenuFixApplied);
        Assert.Equal(expected.MenuUpdateVersion, actual.MenuUpdateVersion);
        Assert.Equal(expected.Volume, actual.Volume);
        Assert.Equal(expected.AudioDevice, actual.AudioDevice);
        Assert.Equal(expected.ConfigEditorSearch, actual.ConfigEditorSearch);
        Assert.Equal(expected.Mute, actual.Mute);
        Assert.Equal(expected.StartupFolder, actual.StartupFolder);
        Assert.Equal(expected.RecentFiles, actual.RecentFiles);
        Assert.Equal(expected.WindowLocation, actual.WindowLocation);
        Assert.Equal(expected.WindowPosition, actual.WindowPosition);
        Assert.Equal(expected.WindowSize, actual.WindowSize);
        Assert.Empty(Directory.GetFiles(directory.Path, "settings.xml.*.tmp"));
    }

    [Fact]
    public void LoadMissingSettingsReturnsDefaults()
    {
        using TestDirectory directory = new();

        AppSettings settings = SettingsStore.Load(Path.Combine(directory.Path, "missing.xml"));

        Assert.Equal(100, settings.Volume);
        Assert.Empty(settings.RecentFiles);
        Assert.Equal("no", settings.Mute);
    }

    [Fact]
    public void LoadInvalidSettingsReturnsDefaultsWithoutThrowing()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "settings.xml");
        File.WriteAllText(path, "<invalid>");

        AppSettings settings = SettingsStore.Load(path);

        Assert.Equal(100, settings.Volume);
        Assert.Empty(settings.RecentFiles);
    }
}

public sealed class ConfigurationPathTests
{
    [Fact]
    public void ExistingMpvNetHomeTakesPrecedenceForPlayerConfiguration()
    {
        using TestDirectory directory = new();
        string? originalHome = Environment.GetEnvironmentVariable("MPVNET_HOME");

        try
        {
            Environment.SetEnvironmentVariable("MPVNET_HOME", directory.Path);
            MainPlayer player = new();

            Assert.Equal(AppPaths.WithTrailingSeparator(directory.Path), player.ConfigFolder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MPVNET_HOME", originalHome);
        }
    }

    [Fact]
    public void MissingMpvNetHomeFallsBackToPortableOrDefaultConfiguration()
    {
        string? originalHome = Environment.GetEnvironmentVariable("MPVNET_HOME");

        try
        {
            Environment.SetEnvironmentVariable("MPVNET_HOME", Path.Combine(Path.GetTempPath(), "missing-mpvnet-home-" + Guid.NewGuid().ToString("N")));
            MainPlayer player = new();

            Assert.True(
                string.Equals(player.ConfigFolder, AppPaths.WithTrailingSeparator(AppPaths.PortableConfig), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(player.ConfigFolder, AppPaths.WithTrailingSeparator(AppPaths.DefaultConfig), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MPVNET_HOME", originalHome);
        }
    }
}

public sealed class RuntimeComponentTests
{
    [Fact]
    public void FileDigestUsesLowercaseSha256()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "digest.bin");
        File.WriteAllText(path, "abc", Encoding.ASCII);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", RuntimeComponentFileSystem.GetFileDigest(path));
    }

    [Fact]
    public void FileLockDetectionDistinguishesUnlockedAndLockedFiles()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "locked.bin");
        File.WriteAllText(path, "content");

        Assert.False(RuntimeComponentFileSystem.IsFileLocked(path));
        using (FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.True(RuntimeComponentFileSystem.IsFileLocked(path));
    }

    [Fact]
    public void DeleteIfExistsRemovesFilesAndDirectories()
    {
        using TestDirectory directory = new();
        string file = Path.Combine(directory.Path, "file.bin");
        string nested = Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(file, "file");
        File.WriteAllText(Path.Combine(nested, "child.bin"), "child");

        RuntimeComponentFileSystem.DeleteIfExists(file);
        RuntimeComponentFileSystem.DeleteIfExists(nested);

        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(nested));
    }

    [Fact]
    public async Task MetadataStoreRoundTripsDigestAndTimestamp()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "component.json");

        await RuntimeComponentMetadataStore.SaveAsync(path, "sha256:test", CancellationToken.None);
        RuntimeComponentMetadata? metadata = await RuntimeComponentMetadataStore.LoadAsync(path, CancellationToken.None);

        Assert.NotNull(metadata);
        Assert.Equal("sha256:test", metadata!.Digest);
        Assert.NotEqual(default, metadata.LastCheckedUtc);
    }

    [Fact]
    public async Task MissingMetadataReturnsNull()
    {
        using TestDirectory directory = new();

        RuntimeComponentMetadata? metadata = await RuntimeComponentMetadataStore.LoadAsync(
            Path.Combine(directory.Path, "missing.json"), CancellationToken.None);

        Assert.Null(metadata);
    }

    [Fact]
    public void RuntimeCatalogContainsRequiredComponents()
    {
        string[] files = RuntimeComponentCatalog.Definitions.Select(definition => definition.FileName).ToArray();

        Assert.Equal(["ffmpeg.exe", "ffplay.exe", "ffprobe.exe", "yt-dlp.exe", "mpvnet.com"], files);
        Assert.All(RuntimeComponentCatalog.Definitions, definition =>
        {
            Assert.StartsWith("https://api.github.com/", definition.ReleaseApiUrl);
            Assert.False(string.IsNullOrWhiteSpace(definition.AssetPattern));
            if (definition.Kind == RuntimeComponentDownloadKind.GitHubZip)
                Assert.NotEmpty(definition.ExtractedFiles);
        });
    }

    [Fact]
    public void RuntimePathResolverFindsComponentOnPath()
    {
        using TestDirectory directory = new();
        string component = Path.Combine(directory.Path, "yt-dlp.exe");
        File.WriteAllText(component, "component");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", directory.Path);
            Assert.Equal(component, RuntimeComponentPathResolver.ResolveFromWindowsPath("yt-dlp.exe"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void RuntimeMetadataPathUsesBundleMetadataForZipDownloads()
    {
        RuntimeComponentDefinition bundle = RuntimeComponentCatalog.Definitions
            .First(definition => definition.Kind == RuntimeComponentDownloadKind.GitHubZip);
        RuntimeComponentDefinition direct = RuntimeComponentCatalog.Definitions
            .First(definition => definition.Kind == RuntimeComponentDownloadKind.Direct);

        Assert.EndsWith("ffmpeg-bundle.json", RuntimeComponentPaths.GetMetadataPath(bundle), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(direct.FileName + ".json", RuntimeComponentPaths.GetMetadataPath(direct), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ExtensionServiceTests
{
    [Fact]
    public void MissingExtensionFolderIsIgnored()
    {
        ExtensionService service = new();

        service.LoadFolder(Path.Combine(Path.GetTempPath(), "mpvnet-missing-extension-" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void InvalidExtensionAssemblyRaisesFailureEvent()
    {
        using TestDirectory directory = new();
        string extensionDirectory = Path.Combine(directory.Path, "invalid-extension");
        Directory.CreateDirectory(extensionDirectory);
        File.WriteAllText(Path.Combine(extensionDirectory, "invalid-extension.dll"), "not a managed assembly");
        Exception? failure = null;
        ExtensionService service = new();
        service.UnhandledException += exception => failure = exception;

        service.LoadFolder(directory.Path);

        Assert.NotNull(failure);
        Assert.IsType<BadImageFormatException>(failure);
    }
}

public sealed class PlayerLifecycleTests
{
    [Fact]
    public void DestroyIsIdempotentAndPreventsNewPlayerTasks()
    {
        MainPlayer player = new();
        bool invoked = false;

        player.Destroy();
        player.Destroy();
        player.SchedulePlayerTask(_ => invoked = true);

        Assert.Equal(PlayerLifecycleState.Destroyed, player.LifecycleState);
        Assert.False(invoked);
        Assert.Empty(player.Clients);
        Assert.Equal(IntPtr.Zero, player.MainHandle);
        Assert.Equal(IntPtr.Zero, player.Handle);
    }
}

public sealed class UnicodeTitleTests
{
    [Theory]
    [InlineData("映画 日本語.mkv", "映画 日本語")]
    [InlineData("한국어 드라마.mkv", "한국어 드라마")]
    [InlineData("Фильм русский.mkv", "Фильм Русский")]
    [InlineData("Zażółć gęślą jaźń.mkv", "Zażółć Gęślą Jaźń")]
    public void NormalizeMediaTitlePreservesUnicodeLetters(string input, string expected)
    {
        Assert.Equal(expected, TitleHelp.NormalizeMediaTitle(input));
    }
}

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mpvnet-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, true);
    }
}
