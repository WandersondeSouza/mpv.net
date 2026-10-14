using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using MpvNet.Windows.Native;
using MpvNet.Windows.Services.MediaTransport;
using Windows.Media;
using Windows.Foundation;
using Windows.Storage.Streams;
using Xunit;

[Collection("Windows SMTC")]
public sealed class MediaTransportWindowsTests
{
    [Fact]
    public async Task DefaultThumbnailIsTheEmbeddedApplicationIcon()
    {
        using IRandomAccessStream image = await MediaTransportThumbnailLoader.LoadDefaultIconAsync(TestContext.Current.CancellationToken);
        Assert.True(image.Size > 0);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(image);
        using Stream resource = typeof(MediaTransportThumbnailLoader).Assembly.GetManifestResourceStream("MpvNet.Windows.DefaultIcon.ico")!;
        using var icon = new System.Drawing.Icon(resource, 256, 256);
        Assert.Equal((uint)icon.Width, decoder.PixelWidth);
        Assert.Equal((uint)icon.Height, decoder.PixelHeight);
        using var bitmap = icon.ToBitmap();
        using var expected = new MemoryStream();
        bitmap.Save(expected, System.Drawing.Imaging.ImageFormat.Png);
        image.Seek(0);
        using var reader = new DataReader(image);
        await reader.LoadAsync((uint)image.Size);
        byte[] actual = new byte[image.Size];
        reader.ReadBytes(actual);
        Assert.Equal(expected.ToArray(), actual);
        reader.DetachStream();
    }

    [Fact]
    public async Task LocalImageIsDecodedAndBadOrMissingImagesRemainOptional()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mpvnet-cover-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(path, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aP1sAAAAASUVORK5CYII="), TestContext.Current.CancellationToken);
            var metadata = new MediaTransportMetadata("Track", MediaTransportMediaType.Music, ThumbnailPath: path);
            using (IRandomAccessStream? image = await MediaTransportThumbnailLoader.LoadAsync(metadata, TestContext.Current.CancellationToken))
                Assert.NotNull(image);
            await File.WriteAllTextAsync(path, "not an image", TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<Exception>(() => MediaTransportThumbnailLoader.LoadAsync(metadata, TestContext.Current.CancellationToken));
            File.Delete(path);
            Assert.Null(await MediaTransportThumbnailLoader.LoadAsync(metadata, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void PositionUpdatesPreserveMetadataAndCoverAndMediaChangeClearsThem() => OnWindowThread(handle =>
    {
        int loads = 0;
        using var service = new WindowsSystemMediaTransportService((metadata, _) =>
        {
            Interlocked.Increment(ref loads);
            return Task.FromResult<IRandomAccessStream?>(metadata.LocalMediaPath != null ? new InMemoryRandomAccessStream() : null);
        });
        service.Initialize(handle);
        Assert.True(service.IsAvailable);
        SystemMediaTransportControls controls = SystemMediaTransportControlsInterop.GetForWindow(handle);
        MediaTransportSnapshot snapshot = Snapshot() with
        {
            Metadata = new("Song", MediaTransportMediaType.Music, Artist: "Singer", Album: "Album",
                AlbumArtist: "Album Artist", LocalMediaPath: @"C:\music\song.flac"),
        };
        service.Publish(snapshot);
        Assert.True(SpinWait.SpinUntil(() => controls.DisplayUpdater.Thumbnail != null, TimeSpan.FromSeconds(5)));
        RandomAccessStreamReference cover = controls.DisplayUpdater.Thumbnail;
        for (int i = 1; i <= 5; i++)
            service.Publish(snapshot with { Position = TimeSpan.FromSeconds(i) });
        Assert.Equal(1, Volatile.Read(ref loads));
        Assert.Equal(cover, controls.DisplayUpdater.Thumbnail);
        Assert.Equal("Album Artist", controls.DisplayUpdater.MusicProperties.AlbumArtist);

        service.Publish(snapshot with { Metadata = new("No cover", MediaTransportMediaType.Music) });
        Assert.True(SpinWait.SpinUntil(() => controls.DisplayUpdater.Thumbnail != null, TimeSpan.FromSeconds(5)));
        Assert.NotEqual(cover, controls.DisplayUpdater.Thumbnail);
        Assert.Equal("No cover", controls.DisplayUpdater.MusicProperties.Title);
        Assert.Equal("", controls.DisplayUpdater.MusicProperties.AlbumArtist);
        Assert.True(controls.IsEnabled);
    });

    [Fact]
    public void ThumbnailFailureDoesNotDisableTransport() => OnWindowThread(handle =>
    {
        using var started = new ManualResetEventSlim();
        using var service = new WindowsSystemMediaTransportService((_, _) =>
        {
            started.Set();
            throw new InvalidDataException("bad image");
        });
        service.Initialize(handle);
        service.Publish(Snapshot() with { Metadata = new("Track", MediaTransportMediaType.Music, LocalMediaPath: @"C:\track.flac") });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        SystemMediaTransportControls controls = SystemMediaTransportControlsInterop.GetForWindow(handle);
        Assert.True(controls.IsEnabled);
        Assert.True(controls.IsPauseEnabled);
        Assert.True(SpinWait.SpinUntil(() => controls.DisplayUpdater.Thumbnail != null, TimeSpan.FromSeconds(5)));
    });

    [Fact]
    public void LateThumbnailAfterMediaChangeOrClosingIsDiscarded() => OnWindowThread(handle =>
    {
        using var started = new ManualResetEventSlim();
        var completion = new TaskCompletionSource<IRandomAccessStream?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stream = new TrackingStream();
        using var service = new WindowsSystemMediaTransportService((_, _) =>
        {
            started.Set();
            return completion.Task; // Simulates an API that completes after cancellation.
        });
        service.Initialize(handle);
        service.Publish(Snapshot() with { Metadata = new("Old", MediaTransportMediaType.Music, LocalMediaPath: @"C:\old.flac") });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        service.Publish(Snapshot() with { Metadata = new("New", MediaTransportMediaType.Music) });
        service.Dispose();
        completion.SetResult(stream);
        Assert.True(SpinWait.SpinUntil(() => stream.Disposed, TimeSpan.FromSeconds(5)));
        SystemMediaTransportControls controls = SystemMediaTransportControlsInterop.GetForWindow(handle);
        Assert.False(controls.IsEnabled);
        Assert.Null(controls.DisplayUpdater.Thumbnail);
        service.Publish(Snapshot());
        Assert.False(controls.IsEnabled);
    });

    [Fact]
    public void SeekButtonsFollowCapabilityAndDisposeDisablesSession() => OnWindowThread(handle =>
    {
        using var service = new WindowsSystemMediaTransportService();
        service.Initialize(handle);
        SystemMediaTransportControls controls = SystemMediaTransportControlsInterop.GetForWindow(handle);
        service.Publish(Snapshot());
        Assert.True(controls.IsFastForwardEnabled);
        Assert.True(controls.IsRewindEnabled);
        service.Publish(Snapshot() with { CanSeek = false });
        Assert.False(controls.IsFastForwardEnabled);
        Assert.False(controls.IsRewindEnabled);
        service.Dispose();
        Assert.False(controls.IsEnabled);
        Assert.Equal(MediaPlaybackStatus.Closed, controls.PlaybackStatus);
    });

    static MediaTransportSnapshot Snapshot() => new(true, true, MediaTransportPlaybackStatus.Playing,
        false, true, true, false, false, new("Track", MediaTransportMediaType.Music),
        TimeSpan.FromMinutes(1), TimeSpan.Zero, CanSeek: true);

    sealed class TrackingStream : IRandomAccessStream
    {
        public volatile bool Disposed;
        public void Dispose() => Disposed = true;
        public bool CanRead => !Disposed;
        public bool CanWrite => !Disposed;
        public ulong Position => 0;
        public ulong Size { get; set; }
        public void Seek(ulong position) => throw new NotSupportedException();
        public IRandomAccessStream CloneStream() => throw new NotSupportedException();
        public IInputStream GetInputStreamAt(ulong position) => throw new NotSupportedException();
        public IOutputStream GetOutputStreamAt(ulong position) => throw new NotSupportedException();
        public IAsyncOperation<bool> FlushAsync() => throw new NotSupportedException();
        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options) => throw new NotSupportedException();
        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer) => throw new NotSupportedException();
    }

    static void OnWindowThread(Action<nint> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var window = new Form();
                test(window.Handle); // A real HWND; no visible window or player is opened.
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Windows SMTC test timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

public sealed class TaskbarThumbnailButtonCacheTests
{
    [Fact]
    public void OnlyChangesToButtonsOrThemeRequirePublication()
    {
        var cache = new TaskbarThumbnailButtonCache();
        TaskbarThumbnailButton[] buttons = [new(TaskbarThumbnailButtonId.PlayPause, TaskbarThumbnailIcon.Pause, "Play/Pause", true)];
        Assert.False(cache.IsCurrent(buttons, true));
        cache.Remember(buttons, true);
        Assert.True(cache.IsCurrent(buttons, true));
        Assert.False(cache.IsCurrent(buttons, false));
        foreach (TaskbarThumbnailButton changed in new[]
        {
            buttons[0] with { Icon = TaskbarThumbnailIcon.Play },
            buttons[0] with { Tooltip = "Reproduzir/Pausar" },
            buttons[0] with { IsEnabled = false },
        })
            Assert.False(cache.IsCurrent([changed], true));
        buttons[0] = buttons[0] with { IsEnabled = false };
        Assert.False(cache.IsCurrent(buttons, true)); // Cache owns a copy.
        cache.Remember(buttons, true);
        cache.Invalidate();
        Assert.False(cache.IsCurrent(buttons, true));
    }
}
