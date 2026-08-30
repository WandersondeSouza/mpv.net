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

        RuntimeComponentDefinition mpvnet = RuntimeComponentCatalog.Definitions
            .Single(definition => definition.FileName == "mpvnet.com");
        Assert.Equal("d4b0a80779dc775fb8817afa128a4ddcfe3bd07bca98a9d0c49ba44daf5cb5e3", mpvnet.PublishedDigest);
    }

    [Fact]
    public async Task RuntimeComponentUpdateLockCanBeReleasedAfterAsyncContinuation()
    {
        RuntimeComponentUpdateLock updateLock = await RuntimeComponentUpdateLock.AcquireAsync(CancellationToken.None);
        await Task.Run(updateLock.Dispose);
    }

    [Fact]
    public void RuntimePathResolverFindsComponentOnPath()
    {
        using TestDirectory directory = new();
        string component = Path.Combine(directory.Path, "yt-dlp.exe");
        WriteX64PortableExecutable(component);
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
    public void RuntimeResolverPrefersValidCacheWithoutUsingCurrentDirectory()
    {
        using TestDirectory directory = new();
        string application = Path.Combine(directory.Path, "aplicação com espaços");
        string cache = Path.Combine(directory.Path, "cache");
        string pathDirectory = Path.Combine(directory.Path, "path");
        Directory.CreateDirectory(application);
        Directory.CreateDirectory(cache);
        Directory.CreateDirectory(pathDirectory);
        string cached = Path.Combine(cache, "yt-dlp.exe");
        WriteX64PortableExecutable(cached);
        WriteX64PortableExecutable(Path.Combine(application, "yt-dlp.exe"));
        WriteX64PortableExecutable(Path.Combine(pathDirectory, "yt-dlp.exe"));

        string originalCurrentDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory.Path;
            ComponentResolutionResult result = RuntimeComponentPathResolver.ResolveResult(
                "yt-dlp.exe", application, cache, [pathDirectory]);

            Assert.Equal(ComponentSource.ComponentCache, result.Source);
            Assert.Equal(cached, result.ResolvedPath);
            Assert.True(result.IsValid);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    [Fact]
    public void RuntimeResolverSkipsInvalidCacheAndUsesApplicationDirectory()
    {
        using TestDirectory directory = new();
        string application = Path.Combine(directory.Path, "application");
        string cache = Path.Combine(directory.Path, "cache");
        Directory.CreateDirectory(application);
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "yt-dlp.exe"), "<html>download failed</html>");
        string expected = Path.Combine(application, "yt-dlp.exe");
        WriteX64PortableExecutable(expected);

        ComponentResolutionResult result = RuntimeComponentPathResolver.ResolveResult(
            "yt-dlp.exe", application, cache, []);

        Assert.Equal(ComponentSource.ApplicationDirectory, result.Source);
        Assert.Equal(expected, result.ResolvedPath);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RuntimeResolverRejectsUnsafeFileNamesAndInvalidExecutables()
    {
        using TestDirectory directory = new();
        string html = Path.Combine(directory.Path, "ffmpeg.exe");
        File.WriteAllText(html, "<html>not an executable</html>");

        ComponentResolutionResult invalidFile = RuntimeComponentPathResolver.ResolveResult(
            "..\\ffmpeg.exe", directory.Path, directory.Path, []);
        ComponentResolutionResult invalidExecutable = RuntimeComponentPathResolver.ResolveResult(
            "ffmpeg.exe", directory.Path, directory.Path, []);

        Assert.False(invalidFile.IsValid);
        Assert.False(invalidExecutable.IsValid);
        Assert.Null(invalidExecutable.ResolvedPath);
        Assert.False(RuntimeComponentValidator.IsX64PortableExecutable(html, out _));
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

    [Fact]
    public async Task MetadataStoreWritesWithoutLeavingTemporaryFiles()
    {
        using TestDirectory directory = new();
        string path = Path.Combine(directory.Path, "component.json");

        await RuntimeComponentMetadataStore.SaveAsync(path, new RuntimeComponentMetadata
        {
            Component = "yt-dlp.exe",
            Digest = new string('a', 64),
            FileDigests = new(StringComparer.OrdinalIgnoreCase) { ["yt-dlp.exe"] = new string('b', 64) }
        }, CancellationToken.None);

        RuntimeComponentMetadata? metadata = await RuntimeComponentMetadataStore.LoadAsync(path, CancellationToken.None);
        Assert.Equal("yt-dlp.exe", metadata!.Component);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void RuntimeBundleExtractorRejectsZipSlipAndDuplicateExecutables()
    {
        using TestDirectory directory = new();
        string archive = Path.Combine(directory.Path, "ffmpeg.zip");
        RuntimeComponentDefinition[] definitions = RuntimeComponentCatalog.Definitions
            .Where(definition => definition.Kind == RuntimeComponentDownloadKind.GitHubZip)
            .ToArray();

        using (FileStream stream = File.Create(archive))
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (StreamWriter writer = new(zip.CreateEntry("../ffmpeg.exe").Open()))
                writer.Write("malicious");
        }

        Assert.Throws<InvalidOperationException>(() => RuntimeComponentService.ExtractBundle(archive, directory.Path, definitions));

        using (FileStream stream = File.Create(archive))
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            foreach (string entryName in new[] { "one/bin/ffmpeg.exe", "two/bin/ffmpeg.exe", "bin/ffplay.exe", "bin/ffprobe.exe" })
            {
                using StreamWriter writer = new(zip.CreateEntry(entryName).Open());
                writer.Write("content");
            }
        }

        Assert.Throws<InvalidOperationException>(() => RuntimeComponentService.ExtractBundle(archive, directory.Path, definitions));
    }

    static void WriteX64PortableExecutable(string path)
    {
        byte[] content = new byte[96];
        content[0] = (byte)'M';
        content[1] = (byte)'Z';
        BitConverter.GetBytes(64).CopyTo(content, 60);
        content[64] = (byte)'P';
        content[65] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(content, 68);
        File.WriteAllBytes(path, content);
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

    [Fact]
    public void DestroyRejectsNativeOperations()
    {
        MainPlayer player = new();

        player.Destroy();

        Assert.False(player.TryEnterNativeOperation(out IDisposable? operation));
        Assert.Null(operation);
        Assert.False(player.GetPropertyBool("idle"));
        Assert.Equal(0, player.GetPropertyInt("playlist-pos"));
        Assert.Equal(0L, player.GetPropertyLong("playlist-pos"));
        Assert.Equal(0d, player.GetPropertyDouble("duration"));
        Assert.Empty(player.GetPropertyString("path"));
        Assert.Empty(player.GetPropertyOsdString("path"));

        player.SetPropertyBool("pause", true);
        player.SetPropertyInt("playlist-pos", 1);
        player.SetPropertyLong("wid", 1);
        player.SetPropertyDouble("volume", 50);
        player.SetPropertyString("path", "ignored");
        player.SetOptionString("idle", "yes");
        player.Command("quit");
        player.CommandV("quit");
        Assert.Equal("property expansion error", player.Expand("${path}"));

        player.ObservePropertyInt("playlist-pos", _ => { });
        Assert.Empty(player.IntPropChangeActions);
    }

    [Fact]
    public void DestroyWaitsForActiveNativeOperation()
    {
        MainPlayer player = new();
        Assert.True(player.TryEnterNativeOperation(out IDisposable? operation));

        Task destroyTask = Task.Run(player.Destroy);
        Assert.False(destroyTask.Wait(TimeSpan.FromMilliseconds(100)));

        operation!.Dispose();

        Assert.True(destroyTask.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(PlayerLifecycleState.Destroyed, player.LifecycleState);
    }

    [Fact]
    public void DestroyWaitsForPendingPropertyTaskAndRejectsItsLateRead()
    {
        MainPlayer player = new();
        using ManualResetEventSlim taskStarted = new();
        using ManualResetEventSlim releaseTask = new();
        string latePropertyValue = "not-finished";

        player.SchedulePlayerTask(_ =>
        {
            taskStarted.Set();
            releaseTask.Wait();
            latePropertyValue = player.GetPropertyString("path");
        });

        Assert.True(taskStarted.Wait(TimeSpan.FromSeconds(2)));
        Task destroyTask = Task.Run(player.Destroy);
        Assert.False(destroyTask.Wait(TimeSpan.FromMilliseconds(100)));

        releaseTask.Set();

        Assert.True(destroyTask.Wait(TimeSpan.FromSeconds(2)));
        Assert.Empty(latePropertyValue);
    }

    [Fact]
    public void EventLoopsFinishBeforeDestroy()
    {
        MainPlayer player = new();
        using CancellationTokenSource cancellation = new();
        Task clientLoop = Task.Run(() => player.EventLoop(cancellation.Token));
        Task mainLoop = Task.Run(() => player.MainEventLoop(cancellation.Token));
        player.TrackEventTask(clientLoop);
        player.TrackEventTask(mainLoop);

        Assert.True(Task.WhenAll(clientLoop, mainLoop).Wait(TimeSpan.FromSeconds(2)));
        player.Destroy();

        Assert.True(clientLoop.IsCompletedSuccessfully);
        Assert.True(mainLoop.IsCompletedSuccessfully);
    }
    [Fact]

    public void DestroyUsesSingleNativeHandleDestruction()
    {
        TrackingPlayer player = new()
        {
            Handle = (nint)1,
            MainHandle = (nint)1
        };

        player.Destroy();

        Assert.Equal(1, player.DestroyNativeHandleCallCount);
        Assert.Equal(IntPtr.Zero, player.Handle);
        Assert.Equal(IntPtr.Zero, player.MainHandle);
    }

    sealed class TrackingPlayer : MainPlayer
    {
        public int DestroyNativeHandleCallCount { get; private set; }

        protected override void DestroyNativeHandle(nint handle)
        {
            DestroyNativeHandleCallCount++;
        }
    }
}

public sealed class PlayerPlaybackRecoveryTests
{
    [Fact]
    public void AutoLoadFolderRequestIsConsumedOnlyOnce()
    {
        MainPlayer player = new();

        player.ArmAutoLoadFolder(true);

        Assert.True(player.TryConsumeAutoLoadFolderRequest());
        Assert.False(player.TryConsumeAutoLoadFolderRequest());

        player.FinishAutoLoadFolder();
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 1, 3)]
    public void PlaybackErrorAdvancesWhenThereIsAnotherPlaylistItem(int failedPosition, int currentPosition, int playlistCount)
    {
        Assert.True(MainPlayer.ShouldAdvanceAfterPlaybackError(failedPosition, currentPosition, playlistCount));
    }

    [Theory]
    [InlineData(2, 2, 3)]
    [InlineData(1, 0, 3)]
    [InlineData(-1, -1, 3)]
    public void PlaybackErrorDoesNotAdvanceWhenRecoveryIsUnsafe(int failedPosition, int currentPosition, int playlistCount)
    {
        Assert.False(MainPlayer.ShouldAdvanceAfterPlaybackError(failedPosition, currentPosition, playlistCount));
    }

    [Fact]
    public void AutoCreatedPlaylistNormalizationDoesNotReloadActivePlayback()
    {
        Assert.False(MainPlayer.ShouldNormalizeAutocreatedPlaylist(3, playbackActive: true));
        Assert.False(MainPlayer.ShouldNormalizeAutocreatedPlaylist(1, playbackActive: false));
        Assert.True(MainPlayer.ShouldNormalizeAutocreatedPlaylist(3, playbackActive: false));
    }
}

public sealed class PlayerStateTests
{
    [Fact]
    public void NewPlayerExposesStableDefaultState()
    {
        MainPlayer player = new();

        Assert.Equal(PlayerLifecycleState.Created, player.LifecycleState);
        Assert.Equal(-1, player.PlaylistPos);
        Assert.Equal(-1, player.Screen);
        Assert.True(player.Border);
        Assert.True(player.TitleBar);
        Assert.True(player.TaskbarProgress);
        Assert.Equal(0.6f, player.Autofit);
        Assert.Equal(0.3f, player.AutofitSmaller);
        Assert.Equal(0.8f, player.AutofitLarger);
        Assert.Empty(player.Path);
        Assert.Empty(player.VID);
        Assert.Empty(player.AID);
        Assert.Empty(player.SID);
    }

    [Fact]
    public void ProcessPropertyUpdatesStateOwnedByPlayer()
    {
        MainPlayer player = new();

        player.ProcessProperty("border", "no");
        player.ProcessProperty("fullscreen", "yes");
        player.ProcessProperty("screen", "2");
        player.ProcessProperty("autofit", "75%");
        player.ProcessProperty("gpu-api", "vulkan");

        Assert.False(player.Border);
        Assert.True(player.Fullscreen);
        Assert.Equal(2, player.Screen);
        Assert.Equal(0.75f, player.Autofit);
        Assert.Equal("vulkan", player.GPUAPI);
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
