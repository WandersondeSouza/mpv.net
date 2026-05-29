using System;
using System.Linq;

using MpvNet;

var tests = new (string Name, bool Result)[]
{
    ("IsVideoFile .mp4", FileTypes.IsVideoFile(".mp4")),
    ("IsVideoFile .mkv", FileTypes.IsVideoFile(".mkv")),
    ("IsPlaylistFile .m3u8", FileTypes.IsPlaylistFile(".m3u8")),
    ("IsStreamingUrl https HLS", FileTypes.IsStreamingUrl("https://example.com/live.m3u8")),
    ("IsStreamingUrl rtmp", FileTypes.IsStreamingUrl("rtmp://server/live")),
    ("IsStreamingUrl rtsp", FileTypes.IsStreamingUrl("rtsp://server/stream")),
    ("IsStreamingUrl udp", FileTypes.IsStreamingUrl("udp://239.0.0.1:1234")),
    ("URL query string supported", FileTypes.IsSupportedMediaInput("https://example.com/live/index.m3u8?token=abc123")),
    ("Unknown file false", !FileTypes.IsSupportedMediaInput("example.unknown")),
    ("Empty text false", !FileTypes.IsSupportedMediaInput("")),
    ("URL does not depend on File.Exists", FileTypes.IsSupportedMediaInput("https://example.com/video.mp4")),
    ("Command line accepts streaming URL", CommandLine.IsLoadableFileArgument("rtmps://example.com/live")),
    ("Command line accepts playlist file extension", CommandLine.IsLoadableFileArgument("iptv.m3u")),
};

var failed = tests.Where(test => !test.Result).ToArray();

foreach (var test in tests)
    Console.WriteLine($"{(test.Result ? "PASS" : "FAIL")} {test.Name}");

if (failed.Length > 0)
    throw new Exception($"{failed.Length} media input support tests failed.");
