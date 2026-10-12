using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Xunit;

namespace MpvNet.Tests;

public sealed class NetworkCachePolicyTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=VIDEO")]
    [InlineData("https://www.bilibili.com/video/BV1Example/")]
    [InlineData("https://www.nicovideo.jp/watch/sm9")]
    [InlineData("https://tv.naver.com/v/1234")]
    [InlineData("https://www.dailymotion.com/video/xexample")]
    [InlineData("https://www.twitch.tv/videos/1234")]
    [InlineData("https://vimeo.com/1234")]
    [InlineData("https://soundcloud.com/artist/track")]
    [InlineData("https://another-extractor.example/watch/1234")]
    public void ServicePagesUseResilientWithoutADomainAllowlist(string path)
    {
        WithProfile("resilient", () =>
        {
            Assert.Equal(NetworkMediaKind.OnlineResolver, MediaInputClassifier.Classify(path).NetworkKind);
            NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(path, new HashSet<string>());
            Assert.Equal("resilient", resolution.Profile);
            Assert.Contains("demuxer-max-bytes=256MiB", resolution.Options);
            Assert.Contains("cache-pause-initial=yes", resolution.Options);
            Assert.Contains("cache-pause-wait=5", resolution.Options);
            Assert.DoesNotContain("cache-on-disk", resolution.Options);
        });
    }

    [Theory]
    [InlineData("low-latency", "64MiB", "no", null)]
    [InlineData("balanced", "128MiB", "yes", "3")]
    [InlineData("resilient", "256MiB", "yes", "5")]
    public void ResolverAndDirectHttpRespectEachProfile(string profile, string bytes, string initial, string? wait)
    {
        WithProfile(profile, () =>
        {
            foreach (string path in new[]
            {
                "https://youtu.be/VIDEO", "https://www.youtube.com/watch?v=VIDEO&list=LIST&index=6",
                "https://unknown.example/media/opaque", "https://example.com/video.mp4",
                "https://example.com/manifest.mpd", "ytdl://https://youtu.be/VIDEO"
            })
            {
                string options = NetworkCachePolicy.Resolve(path, new HashSet<string>()).Options;
                Assert.Contains("demuxer-max-bytes=" + bytes, options);
                Assert.Contains("cache-pause-initial=" + initial, options);
                if (wait is null)
                    Assert.DoesNotContain("cache-pause-wait", options);
                else
                    Assert.Contains("cache-pause-wait=" + wait, options);
                Assert.DoesNotContain("cache-on-disk", options);
            }
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void MissingOrInvalidProfileUsesResilient(string? profile)
    {
        Assert.Equal("resilient", NetworkCachePolicy.NormalizeProfile(profile));
        Assert.Equal("resilient", new AppClass().NetworkCacheProfile);
    }

    [Fact]
    public void LocalFilesAndDisabledPolicyDoNotApplyFileOptions()
    {
        WithProfile("off", () => Assert.Empty(NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", new HashSet<string>()).Options));
        WithProfile("resilient", () =>
        {
            Assert.Empty(NetworkCachePolicy.Resolve(@"C:\Media\video.mp4", new HashSet<string>()).Options);
            Global.App.AutomaticNetworkCache = false;
            Assert.Empty(NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", new HashSet<string>()).Options);
        });
    }

    [Fact]
    public void LocalProfileOrScriptOptionsAreNotOverwrittenByHook()
    {
        WithProfile("resilient", () =>
        {
            NetworkCacheResolution resolution = NetworkCachePolicy.Resolve("https://youtu.be/VIDEO",
                new HashSet<string> { "cache-pause-wait" });
            StringPair[] missing = NetworkCachePolicy.GetMissingFileOptions(resolution,
                name => name is "cache" or "demuxer-max-bytes").ToArray();
            Assert.Single(missing);
            Assert.Equal("cache-pause-initial", missing[0].Name);
            Assert.Equal("yes", missing[0].Value);
        });
    }

    [Fact]
    public void NegativeCommandLineOptionAndUnreadableConfigBlockAutomaticOverride()
    {
        WithProfile("resilient", () =>
        {
            HashSet<string> options = MpvOptionConfiguration.ParseExplicitOptions([new StringPair("no-cache", "")], []);
            Assert.DoesNotContain("cache=yes", NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", options).Options);
            Assert.Empty(NetworkCachePolicy.Resolve("https://youtu.be/VIDEO", new HashSet<string> { "*" }).Options);
        });
    }

    [Theory]
    [InlineData("low-latency", "32MiB")]
    [InlineData("balanced", "64MiB")]
    [InlineData("resilient", "128MiB")]
    public void HlsKeepsItsSmallerBufferAndLiveProtocolsKeepTheirTransportPolicy(string profile, string bytes)
    {
        WithProfile(profile, () =>
        {
            Assert.Contains("demuxer-max-bytes=" + bytes,
                NetworkCachePolicy.Resolve("https://example.com/live.m3u8", new HashSet<string>()).Options);
            string rtsp = NetworkCachePolicy.Resolve("rtsp://example.com/live", new HashSet<string>()).Options;
            Assert.Contains("cache-pause-initial=no", rtsp);
            Assert.Contains("demuxer-max-bytes=32MiB", rtsp);
            Assert.DoesNotContain("network-timeout", rtsp);
        });
    }

    [Fact]
    public void ExplicitCacheOptionsAreReadFromCommandLineConfigDirectory()
    {
        string folder = Path.Combine(Path.GetTempPath(), "mpvnet-cache-conf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        StringPair[] arguments = CommandLine.Arguments.ToArray();
        try
        {
            File.WriteAllText(Path.Combine(folder, "mpv.conf"), "cache=no\ndemuxer-max-bytes=32MiB\n");
            CommandLine.Arguments.Clear();
            CommandLine.Arguments.Add(new StringPair("config-dir", folder));
            HashSet<string> explicitOptions = MpvOptionConfiguration.GetExplicitOptions();
            Assert.Contains("cache", explicitOptions);
            Assert.Contains("demuxer-max-bytes", explicitOptions);
        }
        finally
        {
            CommandLine.Arguments.Clear();
            CommandLine.Arguments.AddRange(arguments);
            File.Delete(Path.Combine(folder, "mpv.conf"));
            Directory.Delete(folder);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"ts-per-stream\":null}")]
    [InlineData("{\"ts-per-stream\":[{\"type\":\"video\",\"cache-duration\":\"https://signed.invalid/?token=secret\"}]}")]
    public void OptionalCacheDiagnosticsIgnoreUnavailableOrUnsafeValues(string json)
    {
        Assert.Empty(MainPlayer.FormatStreamCacheDurations(json));
    }

    [Fact]
    public void CacheDiagnosticsLogOnlyNumericAudioAndVideoDurations()
    {
        const string json = "{\"ts-per-stream\":[{\"type\":\"audio\",\"cache-duration\":5.25},{\"type\":\"video\",\"cache-duration\":4},{\"type\":\"https://signed.invalid/?token=secret\",\"cache-duration\":8}]}";
        Assert.Equal("audio=5.25,video=4", MainPlayer.FormatStreamCacheDurations(json));
    }

    static void WithProfile(string profile, Action test)
    {
        string previous = Global.App.NetworkCacheProfile;
        bool automatic = Global.App.AutomaticNetworkCache;
        try
        {
            Global.App.NetworkCacheProfile = profile;
            Global.App.AutomaticNetworkCache = true;
            test();
        }
        finally
        {
            Global.App.NetworkCacheProfile = previous;
            Global.App.AutomaticNetworkCache = automatic;
        }
    }
}
