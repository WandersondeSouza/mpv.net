using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MpvNet;
using MpvNet.Help;

using Xunit;

namespace MpvNet.Tests;

public sealed class ResourceLifecycleTests
{
    [Fact]
    public void DebugTraceListenerReleasesItsFileAndIsIdempotent()
    {
        using TestDirectory directory = new();
        string logPath = Path.Combine(directory.Path, "MpvNet-debug.log");
        string renamedPath = Path.Combine(directory.Path, "MpvNet-debug-renamed.log");
        using AppClass app = new();

        app.InitializeDebugTraceListener(logPath);
        app.InitializeDebugTraceListener(logPath);
        Trace.WriteLine("resource lifecycle test");
        app.Dispose();
        app.Dispose();

        using (FileStream stream = new(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.True(stream.Length > 0);

        File.Move(logPath, renamedPath);
        File.Delete(renamedPath);
        Assert.False(File.Exists(renamedPath));
    }

    [Fact]
    public void SettingsAndAtomicWritesReleaseFilesAndTemporaryPaths()
    {
        using TestDirectory directory = new();
        string settingsPath = Path.Combine(directory.Path, "settings.xml");
        string textPath = Path.Combine(directory.Path, "input.conf");

        SettingsStore.Save(settingsPath, new AppSettings { Volume = 73 });
        Assert.Equal(73, SettingsStore.Load(settingsPath).Volume);
        AssertFileIsExclusive(settingsPath);
        Assert.Empty(Directory.GetFiles(directory.Path, "settings.xml.*.tmp"));

        FileHelp.WriteAllTextAtomic(textPath, "input-default-bindings=yes", new UTF8Encoding(false));
        AssertFileIsExclusive(textPath);
        Assert.Empty(Directory.GetFiles(directory.Path, "input.conf.*.tmp"));
    }

    [Fact]
    public async Task BackgroundTaskRunnerCapturesExceptionsAndHonorsCancellation()
    {
        InvalidOperationException failure = new("expected");
        Exception? captured = null;

        await BackgroundTaskRunner.RunAsync(
            _ => Task.FromException(failure),
            TestContext.Current.CancellationToken,
            exception => captured = exception);

        Assert.Same(failure, captured);

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        bool invoked = false;
        await BackgroundTaskRunner.RunAsync(
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            cancellation.Token);

        Assert.False(invoked);
    }

    [Fact]
    public void MpvClientDisposeIsIdempotentAndReleasesSubscriptions()
    {
        using MpvClient client = new();
        int notificationCount = 0;
        client.EventQueueOverflow += () => notificationCount++;

        client.Dispose();
        client.Dispose();
        client.OnQueueOverflow();

        Assert.Equal(0, notificationCount);
        Assert.False(client.TryEnterNativeOperation(out IDisposable? operation));
        Assert.Null(operation);
        operation?.Dispose();
    }

    [Fact]
    public void PlayerDestroyCancelsPendingWorkAndRemainsIdempotent()
    {
        using MainPlayer player = new();
        bool reachedAfterCancellation = false;

        player.SchedulePlayerTask(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            reachedAfterCancellation = true;
        });

        player.Destroy();
        player.Destroy();

        Assert.False(reachedAfterCancellation);
        Assert.Equal(PlayerLifecycleState.Destroyed, player.LifecycleState);
    }

    [Fact]
    public void PlayerDestroyCancelsPlaylistNormalizationDebounce()
    {
        using MainPlayer player = new();
        player.ScheduleAutocreatedPlaylistNormalization();

        player.Destroy();

        Assert.False(player.HasPendingPlaylistNormalization);
    }

    static void AssertFileIsExclusive(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
    }
}
