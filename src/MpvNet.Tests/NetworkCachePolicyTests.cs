using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace MpvNet.Tests;

public sealed class NetworkCachePolicyTests
{
    [Theory]
    [InlineData("low-latency", true)]
    [InlineData("balanced", true)]
    [InlineData("resilient", true)]
    [InlineData("off", false)]
    [InlineData("INVALID", true)]
    public void LegacyConfigurationMigratesWithoutChangingUnrelatedLines(string profile, bool enabled)
    {
        string[] lines = ["# User settings", "language=english", "network-cache-profile=" + profile, "queue=yes"];
        string[] migrated = NetworkCacheConfiguration.Migrate(lines);
        Assert.Equal(new[] { lines[0], lines[1], "automatic-network-cache=" + (enabled ? "yes" : "no"), lines[3] }, migrated);
        Assert.Equal(migrated, NetworkCacheConfiguration.Migrate(migrated));
        using AppClass app = new();
        Assert.True(app.ProcessProperty("network-cache-profile", profile));
        Assert.Equal(enabled, app.AutomaticNetworkCache);
    }

    [Theory]
    [InlineData("off", "yes", "no")]
    [InlineData("balanced", "no", "no")]
    [InlineData("resilient", "yes", "yes")]
    public void MigrationPreservesBothOldWaysOfDisablingCacheRegardlessOfLineOrder(string profile, string toggle, string expected)
    {
        string[] lines = ["network-cache-profile=" + profile, "automatic-network-cache=" + toggle];
        Assert.Equal(new[] { "automatic-network-cache=" + expected }, NetworkCacheConfiguration.Migrate(lines));
        Assert.Equal(new[] { "automatic-network-cache=" + expected }, NetworkCacheConfiguration.Migrate(lines.Reverse().ToArray()));
    }

    [Fact]
    public void NewInstallationUsesTheSingleToggleAndCliCanOverrideMigratedSettings()
    {
        using AppClass app = new();
        Assert.True(app.AutomaticNetworkCache);
        app.ProcessProperty("network-cache-profile", "off");
        app.ProcessProperty("automatic-network-cache", "yes");
        Assert.True(app.AutomaticNetworkCache);
        Assert.Same(Array.Empty<string>(), NetworkCacheConfiguration.Migrate(Array.Empty<string>()));
    }

    [Fact]
    public void MigrationAcceptsQuotedProfilesAndKeepsInlineComments()
    {
        string[] lines = [" # ignored=1", "network-cache-profile = 'off' # keep explanation", "language=english"];
        Assert.Equal(new[] { lines[0], "automatic-network-cache=no", "# keep explanation", lines[2] }, NetworkCacheConfiguration.Migrate(lines));
    }

    [Theory]
    [InlineData("cache-secs")]
    [InlineData("demuxer-readahead-secs")]
    public void ExplicitTimeCapPreventsItsCounterpartFromDefeatingIt(string option)
    {
        WithAutomatic(() =>
        {
            var resolution = NetworkCachePolicy.Resolve(new NetworkCacheMedia(NetworkMediaKind.HttpProgressive, true, false), new HashSet<string> { option });
            Assert.DoesNotContain("cache-secs=", resolution.Options);
            Assert.DoesNotContain("demuxer-readahead-secs=", resolution.Options);
        });
    }

    [Theory]
    [InlineData(NetworkMediaKind.HttpProgressive, true, 512, 64, 240)]
    [InlineData(NetworkMediaKind.HttpProgressive, false, 512, 64, 240)]
    [InlineData(NetworkMediaKind.OnlineResolver, true, 512, 64, 240)]
    [InlineData(NetworkMediaKind.OnlineResolver, false, 512, 64, 240)]
    [InlineData(NetworkMediaKind.Dash, true, 512, 64, 240)]
    [InlineData(NetworkMediaKind.Hls, true, 512, 64, 240)]
    [InlineData(NetworkMediaKind.Hls, false, 512, 64, 240)]
    [InlineData(NetworkMediaKind.Dash, false, 512, 64, 240)]
    [InlineData(NetworkMediaKind.Rtsp, false, 128, 16, 90)]
    [InlineData(NetworkMediaKind.Rtmp, false, 128, 16, 90)]
    [InlineData(NetworkMediaKind.DatagramLive, false, 128, 16, 90)]
    [InlineData(NetworkMediaKind.GenericNetwork, false, 64, 8, 70)]
    [InlineData(NetworkMediaKind.FtpFile, true, 512, 64, 240)]
    [InlineData(NetworkMediaKind.SftpFile, true, 512, 64, 240)]
    [InlineData(NetworkMediaKind.FtpFile, false, 512, 64, 240)]
    [InlineData(NetworkMediaKind.SftpFile, false, 512, 64, 240)]
    public void AutomaticPolicyBoundsForwardBackwardAndTimeWithoutEnablingDisk(NetworkMediaKind kind, bool vod, int mib, int back, int seconds)
    {
        WithAutomatic(() =>
        {
            string options = NetworkCachePolicy.Resolve(new NetworkCacheMedia(kind, vod, !vod), new HashSet<string>()).Options;
            Assert.Contains($"demuxer-max-bytes={mib}MiB", options);
            Assert.Contains($"demuxer-max-back-bytes={back}MiB", options);
            Assert.Contains($"cache-pause-initial={(vod ? "yes" : "no")}", options);
            Assert.Contains($"cache-secs={seconds}", options);
            Assert.Contains($"demuxer-readahead-secs={seconds}", options);
            Assert.Contains($"cache-pause-wait={(vod ? 3 : 1)}", options);
            Assert.DoesNotContain("cache-on-disk", options);
            Assert.DoesNotContain("network-timeout", options);
        });
    }

    [Theory]
    [InlineData("https://youtu.be/VIDEO", 64, 70)]
    [InlineData("https://example.com/live.m3u8", 512, 240)]
    [InlineData("https://example.com/opaque", 64, 70)]
    [InlineData("https://example.com/manifest.mpd", 512, 240)]
    public void UnknownPagesAndManifestSuffixesNeverImplyVodOrStartupWait(string path, int mib, int seconds)
    {
        WithAutomatic(() =>
        {
            var resolution = NetworkCachePolicy.Resolve(path, new HashSet<string>());
            Assert.Equal("automatic", resolution.Profile);
            Assert.Contains($"demuxer-max-bytes={mib}MiB", resolution.Options);
            Assert.Contains($"cache-secs={seconds}", resolution.Options);
            Assert.Contains("cache-pause-initial=no", resolution.Options);
        });
    }

    [Theory]
    [InlineData("http", "video.mp4", NetworkMediaKind.HttpProgressive)]
    [InlineData("https", "video.mp4", NetworkMediaKind.HttpProgressive)]
    [InlineData("http", "index.m3u8", NetworkMediaKind.Hls)]
    [InlineData("https", "index.m3u8", NetworkMediaKind.Hls)]
    [InlineData("http", "manifest.mpd", NetworkMediaKind.Dash)]
    [InlineData("https", "manifest.mpd", NetworkMediaKind.Dash)]
    [InlineData("http", "watch", NetworkMediaKind.OnlineResolver)]
    [InlineData("https", "watch", NetworkMediaKind.OnlineResolver)]
    public void HttpAndHttpsShareTheLargeGroupAfterVodIsIdentified(string scheme, string path, NetworkMediaKind expected)
    {
        NetworkCachePolicyTests.WithAutomatic(() =>
        {
            string url = $"{scheme}://example.invalid/{path}";
            Assert.Equal(expected, MediaInputClassifier.Classify(url).NetworkKind);
            var media = NetworkCacheMedia.Identify(url, "", "", 120, true, resolverLive: false);
            string options = NetworkCachePolicy.Resolve(media, new HashSet<string>()).Options;
            Assert.Contains("demuxer-max-bytes=512MiB", options);
            Assert.Contains("demuxer-max-back-bytes=64MiB", options);
            Assert.Contains("cache-secs=240", options);
            Assert.Contains("demuxer-readahead-secs=240", options);
            Assert.Contains("cache-pause-wait=3", options);
        });
    }

    [Fact]
    public void LocalFilesAndDisabledToggleApplyNoOptions()
    {
        WithAutomatic(() =>
        {
            Assert.Empty(NetworkCachePolicy.Resolve(@"C:\Media\video.mp4", new HashSet<string>()).Options);
            Global.App.AutomaticNetworkCache = false;
            Assert.Empty(NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", new HashSet<string>()).Options);
        });
    }

    [Fact]
    public void HookPreservesEachExplicitOptionAndLocalScriptValue()
    {
        WithAutomatic(() =>
        {
            var resolution = NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", new HashSet<string> { "cache-pause-wait" });
            var missing = NetworkCachePolicy.GetMissingFileOptions(resolution, name => name is "cache" or "demuxer-max-bytes").ToArray();
            Assert.DoesNotContain(missing, pair => pair.Name is "cache" or "demuxer-max-bytes" or "cache-pause-wait");
            Assert.Contains(missing, pair => pair.Name == "cache-pause-initial" && pair.Value == "no");
        });
    }

    [Theory]
    [InlineData("include=another.conf")]
    [InlineData("profile=external")]
    [InlineData("use-filedir-conf=yes")]
    public void IndirectConfigurationDefersCompletelyToMpv(string line)
    {
        WithAutomatic(() => Assert.Empty(NetworkCachePolicy.Resolve("https://example.com/video.mp4",
            MpvOptionConfiguration.ParseExplicitOptions([], [line])).Options));
    }

    [Fact]
    public void NegativeCliAndUnreadableConfigBlockOverrides()
    {
        WithAutomatic(() =>
        {
            var options = MpvOptionConfiguration.ParseExplicitOptions([new StringPair("no-cache", "")], []);
            Assert.DoesNotContain("cache=yes", NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", options).Options);
            Assert.Empty(NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", new HashSet<string> { "*" }).Options);
        });
    }

    [Fact]
    public void LoadfileDefersCacheToHookForEveryProtocol()
    {
        foreach (string path in new[] { "https://example.com/video.mp4", "https://example.com/a.m3u8", "ftp://example.com/a", "rtsp://example.com/live" })
        {
            Assert.Equal(new[] { "loadfile", path }, MainPlayer.BuildLoadfileArgs(path, 0, false));
            Assert.Equal(new[] { "loadfile", path, "append" }, MainPlayer.BuildLoadfileArgs(path, 1, true));
        }
    }

    [Fact]
    public void ExplicitOptionsUseTheCommandLineConfigDirectory()
    {
        using TestDirectory directory = new();
        StringPair[] arguments = CommandLine.Arguments.ToArray();
        try
        {
            File.WriteAllText(Path.Combine(directory.Path, "mpv.conf"), "cache=no\ndemuxer-max-bytes=32MiB\n");
            CommandLine.Arguments.Clear();
            CommandLine.Arguments.Add(new StringPair("config-dir", directory.Path));
            var options = MpvOptionConfiguration.GetExplicitOptions();
            Assert.Contains("cache", options);
            Assert.Contains("demuxer-max-bytes", options);
        }
        finally { CommandLine.Arguments.Clear(); CommandLine.Arguments.AddRange(arguments); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"ts-per-stream\":null}")]
    [InlineData("{\"ts-per-stream\":[{\"type\":\"video\",\"cache-duration\":-2}]}")]
    [InlineData("{\"ts-per-stream\":[{\"type\":\"video\",\"cache-duration\":1e999}]}")]
    [InlineData("{\"ts-per-stream\":[{\"type\":\"video\",\"cache-duration\":\"https://signed.invalid/?token=secret\"}]}")]
    public void OptionalDiagnosticsIgnoreUnsafeValues(string json) => Assert.Empty(MainPlayer.FormatStreamCacheDurations(json));

    [Fact]
    public void DiagnosticsRetainOnlyNumericAudioAndVideoDurations()
    {
        const string json = "{\"ts-per-stream\":[{\"type\":\"audio\",\"cache-duration\":5.25},{\"type\":\"video\",\"cache-duration\":4},{\"type\":\"https://signed.invalid/?token=secret\",\"cache-duration\":8}]}";
        Assert.Equal("audio=5.25,video=4", MainPlayer.FormatStreamCacheDurations(json));
        var durations = MainPlayer.ReadStreamCacheDurations(json);
        Assert.Equal(5.25, durations.Audio);
        Assert.Equal(4, durations.Video);
    }

    internal static void WithAutomatic(Action test)
    {
        bool previous = Global.App.AutomaticNetworkCache;
        try { Global.App.AutomaticNetworkCache = true; test(); }
        finally { Global.App.AutomaticNetworkCache = previous; }
    }

    [Fact]
    public void EditorExposesOneAutomaticToggleWithLiteralStableValues()
    {
        var resources = new System.Resources.ResourceManager("MpvNet.Windows.Properties.Resources",
            typeof(MpvNet.Windows.EditorConfigurationService).Assembly);
        var settings = MpvNet.Windows.EditorConfigurationService.ParseSettings(resources.GetString("editor_conf")!);
        var toggle = Assert.IsType<MpvNet.Windows.OptionSetting>(Assert.Single(settings,
            setting => setting.Name == "automatic-network-cache"));
        Assert.Equal("mpvnet", toggle.File);
        Assert.Equal("yes", toggle.Default);
        Assert.Equal(new[] { "yes", "no" }, toggle.Options.Select(option => option.Name));
        Assert.DoesNotContain(settings, setting => setting.Name == "network-cache-profile");
    }
}
