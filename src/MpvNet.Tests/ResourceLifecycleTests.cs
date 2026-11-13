using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MpvNet;
using MpvNet.Help;
using MpvNet.Windows.Help;

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

        if (!Log.DiagnosticFileLoggingEnabled)
        {
            Assert.False(File.Exists(logPath));
            return;
        }

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
    public void AppDisposeRemovesTrackedTemporaryFilesWithoutAPlayerShutdownEvent()
    {
        using TestDirectory directory = new();
        using AppClass app = new();
        string temporaryFile = Path.Combine(directory.Path, "playlist.m3u8");
        File.WriteAllText(temporaryFile, "#EXTM3U");
        app.TempFiles.Add(temporaryFile);

        app.Dispose();

        Assert.False(File.Exists(temporaryFile));
        Assert.Empty(app.TempFiles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void ExecutableProbeCollectsBothRedirectedStreams(int exitCode)
    {
        string executable = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        ExecutableProbeResult result = OnlineMediaDiagnostics.Probe(executable, "/d", "/c",
            $"echo probe-out&echo probe-err 1>&2&exit /b {exitCode}");

        Assert.True(result.Started);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Contains("probe-out", result.Output);
        Assert.Contains("probe-err", result.Output);
        Assert.Equal(exitCode == 0, result.Succeeded);
    }

    [Fact]
    public void ExecutableProbeTimesOutWithBothReadersPending()
    {
        string executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        Stopwatch elapsed = Stopwatch.StartNew();
        ExecutableProbeResult result = OnlineMediaDiagnostics.Probe(executable,
            "-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 30");

        Assert.True(result.Started);
        Assert.Null(result.ExitCode);
        Assert.StartsWith("probe timed out", result.Failure);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(15), $"Probe took {elapsed.Elapsed}.");
    }

    [Fact]
    public void DesktopDpiFallbackReturnsValidDpi()
    {
        Assert.True(WinApiHelp.GetDpi(IntPtr.Zero) > 0);
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

    [Fact]
    public void PlayerShutdownSignalsCompletionAndNotifiesLaterSubscribersAfterFailure()
    {
        using ShutdownProbePlayer player = new();
        bool notified = false;
        player.Shutdown += () => throw new InvalidOperationException("subscriber failed");
        player.Shutdown += () => notified = true;

        player.RaiseShutdown();

        Assert.True(notified);
        Assert.True(player.ShutdownAutoResetEvent.WaitOne(0));
        Assert.Equal(PlayerLifecycleState.Shutdown, player.LifecycleState);
    }

    sealed class ShutdownProbePlayer : MainPlayer
    {
        public void RaiseShutdown() => OnShutdown();
    }

    static void AssertFileIsExclusive(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
    }
}
