using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MpvNet;
using Xunit;

namespace MpvNet.Tests;

public sealed class ApplicationPathTests
{
    [Fact]
    public void PackagedDataUsesFamilyIdentityAndDoesNotDuplicateAnAlreadyRedirectedRoot()
    {
        using TestDirectory directory = new();
        string startup = Path.Combine(directory.Path, "WindowsApps", "Player_1.0_x64", "App");
        string local = Path.Combine(directory.Path, "usuário", "AppData", "Local");
        const string family = "Player_family";
        string expected = Path.Combine(local, "Packages", family, "LocalCache", "Local");
        Assert.Equal(local, AppPaths.ResolveLocalAppData(startup, local, null, null));
        Assert.Equal(expected, AppPaths.ResolveLocalAppData(startup, local, null, family));
        Assert.Equal(expected, AppPaths.ResolveLocalAppData(startup, expected, null, family));
        Assert.Equal(expected, AppPaths.ResolveLocalAppData(startup, expected + "\\", null, family));
        Assert.Equal(expected, AppPaths.ResolveLocalAppData(startup.Replace("1.0", "2.0"), local, null, family));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Player_family")]
    public void RelativeDataOverrideIsAnchoredToTheApplicationAndIndependentOfWorkingDirectory(string? family)
    {
        using TestDirectory directory = new();
        string startup = Path.Combine(directory.Path, "aplicação portátil");
        string expected = Path.Combine(startup, "dados locais");
        string previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Environment.SystemDirectory;
            Assert.Equal(expected, AppPaths.ResolveLocalAppData(startup, directory.Path, "\"dados locais\"", family));
        }
        finally { Environment.CurrentDirectory = previous; }
    }

    [Fact]
    public void ConfigurationSelectionCoversPortableInstalledPackagedAndExplicitOverrides()
    {
        using TestDirectory directory = new();
        string startup = Path.Combine(directory.Path, "program files", "player");
        string portable = Path.Combine(startup, "portable_config");
        string home = Path.Combine(directory.Path, "meu perfil");
        string data = Path.Combine(directory.Path, "LocalCache", "Local", "mpv.net");
        Directory.CreateDirectory(portable);
        Directory.CreateDirectory(home);
        AssertConfig(portable, "portable_config", null, null, false);
        AssertConfig(data, "package-local-data", null, null, true);
        AssertConfig(home, "MPVNET_HOME", "\"" + home + "\"", null, true);
        AssertConfig(Path.Combine(directory.Path, "novo perfil"), "command-line", home, "novo perfil", false);
        AssertConfig(portable, "portable_config", "invalid\0path", null, false);
        Directory.Delete(portable);
        AssertConfig(data, "local-data", null, null, false);
        Assert.Equal("C:\\", AppPaths.WithTrailingSeparator("C:\\"));
        Assert.False(Directory.Exists(data)); // selection and diagnostics do not create writable roots

        void AssertConfig(string expected, string source, string? homeValue, string? cli, bool packaged)
        {
            var result = AppPaths.ResolveConfigurationDirectory(startup, data, directory.Path, homeValue, cli, packaged);
            Assert.Equal(AppPaths.WithTrailingSeparator(expected), result.Path);
            Assert.Equal(source, result.Source);
        }
    }

    [Fact]
    public void PlayerAndFrontendConfigFilesHonorTheLastConfigDirBeforeInitialization()
    {
        using TestDirectory directory = new();
        StringPair[] previous = CommandLine.Arguments.ToArray();
        try
        {
            CommandLine.Arguments.Clear();
            CommandLine.Arguments.Add(new("config-dir", Path.Combine(directory.Path, "old")));
            string selected = Path.Combine(directory.Path, "perfil 日本語");
            CommandLine.Arguments.Add(new("config-dir", selected));
            using MainPlayer player = new();
            Assert.Equal(AppPaths.WithTrailingSeparator(selected), player.ConfigFolder);
            Assert.Equal(Path.Combine(selected, "mpv.conf"), player.ConfPath);
            Assert.True(Directory.Exists(selected));
            Assert.False(Directory.Exists(Path.Combine(directory.Path, "old")));
        }
        finally
        {
            CommandLine.Arguments.Clear();
            CommandLine.Arguments.AddRange(previous);
        }
    }

    [Fact]
    public void PathDiagnosticDoesNotCreateTheSelectedConfigurationDirectory()
    {
        using TestDirectory directory = new();
        string selected = Path.Combine(directory.Path, "not-created");
        string report = AppPaths.DiagnosePaths(["--config-dir=" + selected]);
        Assert.Contains("configuration=" + AppPaths.WithTrailingSeparator(selected), report);
        Assert.Contains("settings=" + Path.Combine(selected, "settings.xml"), report);
        Assert.Contains("frontend-input-conf=" + Path.Combine(selected, "input.conf"), report);
        Assert.Contains("explicit native file/cache overrides are applied by mpv", report);
        Assert.Contains("components-staging=" + AppPaths.ComponentTemp, report);
        Assert.False(Directory.Exists(selected));
    }

    [Fact]
    public void PathEditingComparesWholeEntriesAndPreservesOtherDirectoriesVerbatim()
    {
        const string path = "C:\\tools\\player-old;\"c:\\TOOLS\\PLAYER\\\";C:\\tools\\player\\bin;;C:\\other";
        Assert.True(WindowsPathEnvironment.ContainsEntry(path, "C:\\tools\\player"));
        Assert.False(WindowsPathEnvironment.ContainsEntry("C:\\tools\\player-old", "C:\\tools\\player"));
        Assert.Equal("C:\\tools\\player-old;C:\\tools\\player\\bin;;C:\\other",
            WindowsPathEnvironment.RemoveEntry(path, "C:\\tools\\player"));
        string updated = WindowsPathEnvironment.PrependEntries(path, "C:\\tools\\player");
        Assert.Equal("C:\\tools\\player;C:\\tools\\player-old;C:\\tools\\player\\bin;;C:\\other", updated);
        Assert.Equal(updated, WindowsPathEnvironment.PrependEntries(updated, "C:\\tools\\player\\"));
    }

    [Fact]
    public void ResolverNormalizesQuotedAndExpandedPathEntriesButSkipsRelativeAndMalformedOnes()
    {
        using TestDirectory directory = new();
        string bin = Path.Combine(directory.Path, "ferramentas 日本語");
        Directory.CreateDirectory(bin);
        string expected = Path.Combine(bin, "deno.exe");
        WritePe(expected);
        string? previous = Environment.GetEnvironmentVariable("MPVNET_TEST_BIN");
        try
        {
            Environment.SetEnvironmentVariable("MPVNET_TEST_BIN", bin);
            var result = RuntimeComponentPathResolver.ResolveResult("deno.exe", directory.Path, directory.Path,
                ["relative", "C:\\invalid\0", "\"%MPVNET_TEST_BIN%\\\""]);
            Assert.Equal(expected, result.ResolvedPath);
            Assert.Equal(ComponentSource.EnvironmentPath, result.Source);
        }
        finally { Environment.SetEnvironmentVariable("MPVNET_TEST_BIN", previous); }
    }

    [Fact]
    public void ComponentFallbackUsesOnlyTheSuppliedCurrentLegacyAndApplicationDirectories()
    {
        using TestDirectory directory = new();
        string app = Path.Combine(directory.Path, "app");
        string legacy = Path.Combine(directory.Path, "Component");
        string current = Path.Combine(legacy, "current");
        foreach (string folder in new[] { app, legacy, current })
        {
            Directory.CreateDirectory(folder);
            WritePe(Path.Combine(folder, "deno.exe"));
        }
        foreach (string expected in new[] { current, legacy, app })
        {
            var result = RuntimeComponentPathResolver.ResolveResult("deno.exe", app, current, [], legacy);
            Assert.Equal(Path.Combine(expected, "deno.exe"), result.ResolvedPath);
            File.Delete(result.ResolvedPath!);
        }
    }

    [Fact]
    public void RelativePathEntryCannotResolveAnExecutableFromTheWorkingDirectory()
    {
        using TestDirectory directory = new();
        WritePe(Path.Combine(directory.Path, "deno.exe"));
        string previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory.Path;
            var result = RuntimeComponentPathResolver.ResolveResult("deno.exe",
                Path.Combine(directory.Path, "missing-app"), Path.Combine(directory.Path, "missing-cache"), ["."]);
            Assert.Null(result.ResolvedPath);
        }
        finally { Environment.CurrentDirectory = previous; }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BundledMediaInfoWinsOverAnOldCacheAndRetainsFallbackWhenMissing(bool bundled)
    {
        using TestDirectory directory = new();
        string app = Path.Combine(directory.Path, "app");
        string cache = Path.Combine(directory.Path, "cache");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(cache);
        WritePe(Path.Combine(cache, "MediaInfo.dll"));
        if (bundled)
            WritePe(Path.Combine(app, "MediaInfo.dll"));
        var result = RuntimeComponentPathResolver.ResolveResult("MediaInfo.dll", app, cache, []);
        Assert.Equal(Path.Combine(bundled ? app : cache, "MediaInfo.dll"), result.ResolvedPath);
        Assert.Equal(bundled ? ComponentSource.ApplicationDirectory : ComponentSource.ComponentCache, result.Source);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("yt-dlp.exe:stream")]
    [InlineData("nested/yt-dlp.exe")]
    [InlineData("invalid\0.exe")]
    public void ComponentNamesCannotEscapeTheirDirectoryOrNameAnAlternateDataStream(string name)
    {
        using TestDirectory directory = new();
        Assert.Null(RuntimeComponentPathResolver.ResolveResult(name, directory.Path, directory.Path, []).ResolvedPath);
        Assert.Equal("", RuntimeComponents.ResolveComponentPath(name));
    }

    [Fact]
    public void ChildExecutableSearchFindsLegacyDenoWhenCurrentExistsWithoutIt()
    {
        using TestDirectory directory = new();
        string legacy = Path.Combine(directory.Path, "component");
        string current = Path.Combine(legacy, "current");
        Directory.CreateDirectory(current);
        WritePe(Path.Combine(legacy, "deno.exe"));
        string search = WindowsPathEnvironment.PrependEntries(Environment.SystemDirectory, current, legacy, directory.Path);
        using Process process = new();
        process.StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "where.exe"))
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = current
        };
        process.StartInfo.ArgumentList.Add("deno.exe");
        process.StartInfo.Environment["PATH"] = search;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(5000));
        Assert.Equal(0, process.ExitCode);
        Assert.Contains(Path.Combine(legacy, "deno.exe"), output, StringComparison.OrdinalIgnoreCase);
    }

    static void WritePe(string path)
    {
        byte[] bytes = new byte[96];
        bytes[0] = (byte)'M'; bytes[1] = (byte)'Z';
        BitConverter.GetBytes(64).CopyTo(bytes, 60);
        bytes[64] = (byte)'P'; bytes[65] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(bytes, 68);
        File.WriteAllBytes(path, bytes);
    }
}
