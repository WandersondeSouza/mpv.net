using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace MpvNet.Tests;

public sealed class MediaInputTests
{
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
}
