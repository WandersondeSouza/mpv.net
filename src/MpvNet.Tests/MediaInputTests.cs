using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace MpvNet.Tests;

public sealed class MediaInputTests
{
    public static TheoryData<string> MultiProviderUrls => new()
    {
        "https://www.bilibili.com/video/BV_TEST?p=2&from=search",
        "https://space.bilibili.com/123/channel/collectiondetail?sid=456&ctype=0",
        "https://www.nicovideo.jp/watch/sm_TEST?from=日本語",
        "https://tv.naver.com/v/TEST?query=한국어",
        "https://www.dailymotion.com/video/TEST?playlist=LIST#français",
        "https://www.douyin.com/video/TEST?previous_page=web_code_link",
        "https://www.douyu.com/TEST?rid=直播",
        "https://www.twitch.tv/example?collection=TEST",
        "https://vimeo.com/123456789?share=copy",
        "https://soundcloud.com/example/track?in=example/sets/list",
        "https://media.example.invalid/watch/新しい?future=a%2Bb%3D%3D#章"
    };

    public static TheoryData<string> PreservedUrls => new()
    {
        "https://www.youtube.com/watch?v=VIDEO_ID",
        "https://www.youtube.com/watch?v=VIDEO_ID&t=120s",
        "https://www.youtube.com/watch?v=VIDEO_ID&start=120",
        "https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&index=3",
        "https://www.youtube.com/watch?v=VIDEO_ID&pp=abc%3Ddef#chapter",
        "https://youtu.be/VIDEO_ID?si=abc&t=45",
        "https://example.com/áudio/节目.m3u8?token=a%2Bb%3D%3D&future=x=y#live",
        "rtsp://example.com/live?transport=tcp",
        "rtmp://example.com/app/stream",
        "srt://example.com:9000?mode=caller"
    };

    [Theory]
    [MemberData(nameof(PreservedUrls))]
    public void NormalizePreservesCompleteUrl(string input)
    {
        MediaLoadRequest? request = MediaInputNormalizer.Normalize(input, MediaInputSource.CommandLine);

        Assert.NotNull(request);
        Assert.Equal(input, request.Input);
        Assert.Equal(MediaInputSource.CommandLine, request.Source);
    }

    [Theory]
    [MemberData(nameof(MultiProviderUrls))]
    public void GenericOnlinePipelinePreservesProviderAndFutureExtractorUrls(string input)
    {
        MediaLoadRequest? request = MediaInputNormalizer.Normalize(
            input, MediaInputSource.InterProcessMessage);
        MediaInputClassification classification = MediaInputClassifier.Classify(input);
        string payload = MediaIpcMessage.Serialize("single", [input]);
        bool parsed = MediaIpcMessage.TryParse(payload, out string mode, out string[] ipcArguments);
        string[] loadfileArguments = MainPlayer.BuildLoadfileArgs(input, 0, false);

        Assert.NotNull(request);
        Assert.Equal(input, request.Input);
        Assert.Equal(NetworkMediaKind.OnlineResolver, classification.NetworkKind);
        Assert.True(parsed);
        Assert.Equal("single", mode);
        Assert.Equal([input], ipcArguments);
        Assert.Equal(input, loadfileArguments[1]);
        Assert.DoesNotContain(loadfileArguments, value =>
            value.Contains("yes-playlist", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://cdn.example.com/video.mp4?token=SECRET", NetworkMediaKind.HttpProgressive)]
    [InlineData("https://cdn.example.com/audio.flac", NetworkMediaKind.HttpProgressive)]
    [InlineData("https://cdn.example.com/live.m3u8?token=SECRET", NetworkMediaKind.Hls)]
    [InlineData("https://cdn.example.com/manifest.mpd", NetworkMediaKind.Dash)]
    [InlineData("https://future.example.com/media/opaque-id", NetworkMediaKind.OnlineResolver)]
    public void HttpClassificationKeepsDirectMediaSeparateFromResolverPages(
        string input,
        NetworkMediaKind expected)
    {
        Assert.Equal(expected, MediaInputClassifier.Classify(input).NetworkKind);
    }

    [Theory]
    [MemberData(nameof(MultiProviderUrls))]
    public void ClipboardPreservesRegionalOnlineUrls(string input)
    {
        MediaLoadRequest request = Assert.Single(ClipboardMediaParser.ParseText(input));

        Assert.Equal(input, request.Input);
        Assert.Equal(MediaInputSource.Clipboard, request.Source);
    }

    [Theory]
    [InlineData("\"https://youtu.be/VIDEO_ID?si=abc&t=45\"", "https://youtu.be/VIDEO_ID?si=abc&t=45")]
    [InlineData("'C:\\Mídia\\filme com espaço.mkv'", "C:\\Mídia\\filme com espaço.mkv")]
    public void NormalizeRemovesOnlyMatchingExternalQuotes(string input, string expected)
    {
        MediaLoadRequest? request = MediaInputNormalizer.Normalize(input, MediaInputSource.DragAndDrop);

        Assert.NotNull(request);
        Assert.Equal(expected, request.Input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--fullscreen")]
    [InlineData("https://example.com/video.mp4\n--quit")]
    public void NormalizeRejectsClearlyInvalidInput(string input)
    {
        Assert.Null(MediaInputNormalizer.Normalize(input, MediaInputSource.InterProcessMessage));
    }

    [Fact]
    public void NormalizeManyKeepsOrderAndOrigin()
    {
        string[] inputs = ["one.mp4", "https://example.com/two.m3u8?x=1&y=2"];

        IReadOnlyList<MediaLoadRequest> requests = MediaInputNormalizer.NormalizeMany(
            inputs, MediaInputSource.FileDialog, append: true);

        Assert.Equal(inputs, requests.Select(request => request.Input));
        Assert.All(requests, request =>
        {
            Assert.Equal(MediaInputSource.FileDialog, request.Source);
            Assert.True(request.Append);
        });
    }

    [Fact]
    public void RecentEntryKeepsLegacyDisplayTitleOutsideTheMediaValue()
    {
        MediaLoadRequest? request = MediaInputNormalizer.Normalize(
            "https://example.com/video.mp4?x=1&y=2|Example title",
            MediaInputSource.RecentFiles);

        Assert.NotNull(request);
        Assert.Equal("https://example.com/video.mp4?x=1&y=2", request.Input);
        Assert.Equal("Example title", request.Title);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=VIDEO_ID", false, null)]
    [InlineData("https://youtu.be/VIDEO_ID?si=abc&t=45", false, null)]
    [InlineData("https://www.youtube.com/playlist?list=PLAYLIST_ID", true, null)]
    [InlineData("https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID", true, null)]
    [InlineData("https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&index=3", true, 3)]
    [InlineData("https://music.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&index=12", true, 12)]
    public void YouTubePolicyDetectsExplicitPlaylistIntent(string input, bool expectedPlaylist, int? expectedIndex)
    {
        YouTubeUrlInfo info = YouTubeMediaPolicy.Analyze(input);

        Assert.True(info.IsYouTube);
        Assert.Equal(expectedPlaylist, info.RequestsPlaylist);
        Assert.Equal(expectedIndex, info.RequestedIndex);
    }

    [Theory]
    [InlineData("https://youtube.com.evil.example/watch?v=VIDEO_ID&list=PLAYLIST_ID")]
    [InlineData("https://example.com/watch?v=VIDEO_ID&list=PLAYLIST_ID")]
    public void YouTubePolicyRejectsLookalikeHosts(string input)
    {
        Assert.False(YouTubeMediaPolicy.Analyze(input).IsYouTube);
    }

    [Fact]
    public void YouTubeUsesResolverCacheClassificationInsteadOfProgressiveHttp()
    {
        MediaInputClassification classification = MediaInputClassifier.Classify(
            "https://www.youtube.com/watch?v=VIDEO_ID");

        Assert.Equal(NetworkMediaKind.OnlineResolver, classification.NetworkKind);
        Assert.Contains("demuxer-max-bytes=64MiB", NetworkCachePolicy.Resolve(
            "https://www.youtube.com/watch?v=VIDEO_ID", new HashSet<string>()).Options,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitOptionsAreParsedOnceAndOverrideAutomaticPolicy()
    {
        HashSet<string> explicitOptions = MpvOptionConfiguration.ParseExplicitOptions(
            [new StringPair("cache-pause-wait", "9")],
            ["# ignored", "no-cache", "demuxer-max-bytes=32MiB"]);

        NetworkCacheResolution resolution = NetworkCachePolicy.Resolve(
            "https://example.com/video.mp4", explicitOptions);

        Assert.Contains("cache-pause-initial=yes", resolution.Options, StringComparison.Ordinal);
        Assert.DoesNotContain("cache=yes", resolution.Options, StringComparison.Ordinal);
        Assert.DoesNotContain("cache-pause-wait", resolution.Options, StringComparison.Ordinal);
        Assert.DoesNotContain("demuxer-max-bytes", resolution.Options, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadfileEnablesNativeExpansionOnlyForExplicitYouTubePlaylist()
    {
        const string video = "https://www.youtube.com/watch?v=VIDEO_ID&t=45";
        const string playlist = "https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID&index=3";

        string[] videoArgs = MainPlayer.BuildLoadfileArgs(video, 0, false);
        string[] playlistArgs = MainPlayer.BuildLoadfileArgs(playlist, 0, false);

        Assert.DoesNotContain(videoArgs, value => value.Contains("yes-playlist", StringComparison.Ordinal));
        Assert.Equal(playlist, playlistArgs[1]);
        Assert.Contains(playlistArgs, value => value.Contains(
            YouTubeMediaPolicy.NativePlaylistLoadOption, StringComparison.Ordinal));
    }

    [Fact]
    public void IpcRoundTripPreservesSpecialAndMultipleUrls()
    {
        string[] inputs =
        [
            "https://www.youtube.com/watch?v=VIDEO&list=PLAYLIST&index=3&t=45&future=a=b#chapter",
            "https://example.com/áudio/节目.m3u8?token=a%2Bb%3D%3D&x=y=z",
            "\"https://youtu.be/VIDEO?si=abc&t=30\""
        ];

        string payload = MediaIpcMessage.Serialize("queue", inputs);
        bool parsed = MediaIpcMessage.TryParse(payload, out string mode, out string[] output);

        Assert.True(parsed);
        Assert.Equal("queue", mode);
        Assert.Equal(inputs, output);
    }

    [Theory]
    [InlineData("{\"Version\":1,\"Mode\":\"unknown\",\"Arguments\":[]}")]
    [InlineData("{\"Version\":2,\"Mode\":\"single\",\"Arguments\":[]}")]
    [InlineData("not-json")]
    public void IpcParserRejectsUnsupportedPayload(string payload)
    {
        Assert.False(MediaIpcMessage.TryParse(payload, out _, out _));
    }

    [Fact]
    public void IpcSerializerRejectsUnsupportedMode()
    {
        Assert.Throws<ArgumentException>(() => MediaIpcMessage.Serialize("unknown", []));
    }
}
