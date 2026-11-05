using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using MpvNet.Native;
using Xunit;

namespace MpvNet.Tests;

public sealed class OscVisibilityTests
{
    [Theory]
    [InlineData("auto")]
    [InlineData("always")]
    public async Task LoadingMediaFromIdleRestoresOnlyPermanentControlsAndTheirVideoMargin(string visibility)
    {
        using TestDirectory directory = new();
        string options = Path.Combine(directory.Path, "script-opts");
        Directory.CreateDirectory(options);
        string configuration = $"visibility={visibility}\nboxvideo=yes\nsub_margins=yes\n";
        File.WriteAllText(Path.Combine(options, "osc.conf"), configuration);
        using MainPlayer player = new();
        player.MainHandle = LibMpv.mpv_create();
        Assert.NotEqual(IntPtr.Zero, player.MainHandle);
        foreach (var (key, value) in new[] { ("config", "yes"), ("config-dir", directory.Path),
            ("vo", "null"), ("ao", "null"), ("idle", "yes"), ("pause", "yes"),
            ("load-scripts", "no"), ("osc", "no"), ("ytdl", "no") })
            Assert.True(LibMpv.mpv_set_option_string(player.MainHandle,
                Encoding.UTF8.GetBytes(key + "\0"), Encoding.UTF8.GetBytes(value + "\0")) >= 0, key);
        Assert.Equal(0, (int)LibMpv.mpv_initialize(player.MainHandle));
        player.SetMpvInitialized();
        player.Handle = LibMpv.mpv_create_client(player.MainHandle, "osc-tests");
        Assert.NotEqual(IntPtr.Zero, player.Handle);
        player.CommandV("load-script", Path.Combine(AppContext.BaseDirectory, "Scripts", "osc.lua"));
        string ReadVisibility()
        {
            string json = player.GetPropertyString("user-data/osc/visibility");
            return json.Length == 0 ? "" : JsonSerializer.Deserialize<string>(json)!;
        }
        await WaitFor(() => ReadVisibility() == visibility);
        // The script publishes margins during its initial tick, including in
        // headless output. This exercises the distributed Lua, not a C# model.
        await WaitFor(() => player.GetPropertyString("user-data/osc/margins") != "");
        for (int load = 0; load < 2; load++)
        {
            player.CommandV("loadfile", "av://lavfi:testsrc=size=640x360:rate=5");
            await WaitFor(() => !player.GetPropertyBool("idle-active"));
            if (visibility == "always")
                await WaitFor(() => player.GetPropertyDouble("video-margin-ratio-bottom") > 0);
            else
                Assert.Equal(0, player.GetPropertyDouble("video-margin-ratio-bottom"));
            Assert.Equal(visibility, ReadVisibility());
            player.CommandV("stop");
            await WaitFor(() => player.GetPropertyBool("idle-active"));
            await WaitFor(() => player.GetPropertyDouble("video-margin-ratio-bottom") == 0);
        }
        Assert.Equal(configuration, File.ReadAllText(Path.Combine(options, "osc.conf")));
    }

    static async Task WaitFor(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.True(condition(), "The native OSC did not reach the expected state.");
    }
}
