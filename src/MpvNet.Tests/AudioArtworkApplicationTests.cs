using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MpvNet.Tests;

public sealed class AudioArtworkApplicationTests
{
    [Theory]
    [InlineData(false, false, ".wav")]
    [InlineData(false, false, ".ogg")]
    [InlineData(true, false, ".wav")]
    [InlineData(false, true, ".wav")]
    public async Task PlayerDistinguishesMissingPictureFromExplicitlyDisabledVideo(bool disableVideo, bool disableArtwork, string extension)
    {
        using TestDirectory directory = new();
        string media = NativePlaylistTitleTests.WriteWave(directory.Path, "plain" + extension);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "mpvnet.conf"), "audio-artwork-online=no\n",
            TestContext.Current.CancellationToken);
        string pipeName = "mpvnet-artwork-test-" + Guid.NewGuid().ToString("N");
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "mpvnet.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (string argument in new[] { "--config-dir=" + directory.Path, "--process-instance=multi",
            "--input-ipc-server=\\\\.\\pipe\\" + pipeName, "--vo=null", "--ao=null", "--pause=yes", "--keep-open=yes" })
            start.ArgumentList.Add(argument);
        if (disableVideo) start.ArgumentList.Add("--vid=no");
        if (disableArtwork) start.ArgumentList.Add("--audio-display=no");
        start.ArgumentList.Add(media);
        start.Environment["MPVNET_LOCAL_APPDATA"] = Path.Combine(directory.Path, "data");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using Process process = Process.Start(start)!;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeout.Token);
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            int request = 0;
            async Task<JsonElement> Send(params string[] args)
            {
                int id = ++request;
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { command = args, request_id = id }).AsMemory(), timeout.Token);
                while (await reader.ReadLineAsync(timeout.Token) is { } line)
                {
                    using JsonDocument response = JsonDocument.Parse(line);
                    if (response.RootElement.TryGetProperty("request_id", out var result) && result.GetInt32() == id)
                        return response.RootElement.Clone();
                }
                throw new IOException("Artwork test IPC closed.");
            }
            async Task<JsonElement?> Get(string property)
            {
                JsonElement response = await Send("get_property", property);
                return response.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null ? data : null;
            }
            while ((await Get("path"))?.GetString() != media || (await Get("seekable"))?.GetBoolean() != true)
                await Task.Delay(20, timeout.Token);
            if (disableVideo || disableArtwork)
            {
                await Task.Delay(300, timeout.Token);
                Assert.Null(await Get("current-tracks/video"));
                Assert.Equal("none", (await Get("user-data/mpvnet/audio-artwork-source"))?.GetString());
            }
            else
            {
                while ((await Get("user-data/mpvnet/audio-artwork-source"))?.GetString() != "default" ||
                    (await Get("current-tracks/video/albumart"))?.GetBoolean() != true)
                    await Task.Delay(20, timeout.Token);
                string? artwork = (await Get("current-tracks/video/external-filename"))?.GetString();
                Assert.NotNull(artwork);
                Assert.True(File.Exists(artwork));
                Assert.StartsWith(Path.Combine(directory.Path, "data", "mpv.net", "Artwork"), artwork);
            }
            await Send("quit");
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
