using System;
using System.Collections.Generic;
using System.Linq;

using MpvNet.Windows.Services.MediaTransport;
using Xunit;

public sealed class MediaTransportMetadataTests
{
    [Fact]
    public void PrefersSafeMediaTitle()
    {
        MediaTransportMetadata metadata = MediaTransportMetadataBuilder.Build(new(
            Path: @"C:\Media\fallback.mp4",
            MediaTitle: "Título publicado",
            FileName: "fallback.mp4",
            HasVideo: true,
            HasAudio: true));

        Assert.Equal("Título publicado", metadata.Title);
        Assert.Equal(MediaTransportMediaType.Video, metadata.MediaType);
    }

    [Fact]
    public void FallsBackToLocalFileNameWithoutExtension()
    {
        MediaTransportMetadata metadata = MediaTransportMetadataBuilder.Build(new(
            Path: @"C:\Media\video de teste.mp4",
            MediaTitle: null,
            FileName: null,
            HasVideo: true,
            HasAudio: false));

        Assert.Equal("video de teste", metadata.Title);
    }

    [Fact]
    public void DoesNotPublishUrlCredentialsOrQuery()
    {
        MediaTransportMetadata metadata = MediaTransportMetadataBuilder.Build(new(
            Path: "https://user:secret@example.com/live/video.mp4?token=private",
            MediaTitle: "https://user:secret@example.com/live/video.mp4?token=private",
            FileName: null,
            HasVideo: true,
            HasAudio: true));

        Assert.Equal("video", metadata.Title);
        Assert.DoesNotContain("secret", metadata.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", metadata.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublishesAudioTagsWithoutInventingVideoFields()
    {
        MediaTransportMetadata metadata = MediaTransportMetadataBuilder.Build(new(
            Path: "song.flac",
            MediaTitle: "Song",
            FileName: "song",
            HasVideo: false,
            HasAudio: true,
            Artist: "Artist",
            Album: "Album",
            TrackNumber: 7,
            Subtitle: "ignored"));

        Assert.Equal(MediaTransportMediaType.Music, metadata.MediaType);
        Assert.Equal("Artist", metadata.Artist);
        Assert.Equal("Album", metadata.Album);
        Assert.Equal((uint)7, metadata.TrackNumber);
        Assert.Null(metadata.Subtitle);
    }

    [Fact]
    public void PublishesVideoSubtitleAndClearsInvalidTrackNumber()
    {
        MediaTransportMetadata metadata = MediaTransportMetadataBuilder.Build(new(
            Path: "movie.mkv",
            MediaTitle: "Movie",
            FileName: "movie",
            HasVideo: true,
            HasAudio: true,
            TrackNumber: 0,
            Subtitle: "Feature"));

        Assert.Equal(MediaTransportMediaType.Video, metadata.MediaType);
        Assert.Equal("Feature", metadata.Subtitle);
        Assert.Null(metadata.TrackNumber);
    }
}

public sealed class MediaTransportControllerTests
{
    [Fact]
    public void DoesNotAcceptCommandsBeforeMediaIsLoaded()
    {
        var fake = new FakeMediaTransportService();
        var received = new List<MediaTransportCommandEventArgs>();
        using var controller = new MediaTransportController(fake, received.Add);
        controller.Initialize(new nint(1));
        controller.Publish(MediaTransportSnapshot.Disabled);

        fake.Raise(MediaTransportCommand.Play);

        Assert.Empty(received);
    }

    [Fact]
    public void AcceptsPlayWhenPlayIsEnabled()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot() with
        {
            PlaybackStatus = MediaTransportPlaybackStatus.Paused,
            CanPlay = true,
            CanPause = false,
        });
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Play);
            Assert.Equal(MediaTransportCommand.Play, Assert.Single(received).Command);
        }
    }

    [Fact]
    public void AcceptsPauseWhenPauseIsEnabled()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot());
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Pause);
            Assert.Equal(MediaTransportCommand.Pause, Assert.Single(received).Command);
        }
    }

    [Fact]
    public void AcceptsStopWhenStopIsEnabled()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot());
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Stop);
            Assert.Equal(MediaTransportCommand.Stop, Assert.Single(received).Command);
        }
    }

    [Fact]
    public void PreviousIsDisabledAtFirstPlaylistItem()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot() with { CanPrevious = false });
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Previous);
            Assert.Empty(received);
        }
    }

    [Fact]
    public void NextIsDisabledAtLastPlaylistItem()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot() with { CanNext = false });
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Next);
            Assert.Empty(received);
        }
    }

    [Fact]
    public void PreviousAndNextAreEnabledInTheMiddleOfPlaylist()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot());
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Previous);
            fake.Raise(MediaTransportCommand.Next);
            Assert.Equal([MediaTransportCommand.Previous, MediaTransportCommand.Next], received.Select(item => item.Command));
        }
    }

    [Fact]
    public void SingleItemPlaylistDisablesBothNavigationButtons()
    {
        var snapshot = PlayingSnapshot() with { CanPrevious = false, CanNext = false };
        var (controller, fake, received) = CreateController(snapshot);
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Previous);
            fake.Raise(MediaTransportCommand.Next);
            Assert.Empty(received);
            Assert.False(controller.Snapshot.CanPrevious);
            Assert.False(controller.Snapshot.CanNext);
        }
    }

    [Fact]
    public void AcceptsSeekAndClampsItToDuration()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot());
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Seek, TimeSpan.FromMinutes(5));
            MediaTransportCommandEventArgs request = Assert.Single(received);
            Assert.Equal(TimeSpan.FromMinutes(2), request.Position);
        }
    }

    [Fact]
    public void ClampsNegativeSeekToZero()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot());
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Seek, TimeSpan.FromSeconds(-1));
            Assert.Equal(TimeSpan.Zero, Assert.Single(received).Position);
        }
    }

    [Fact]
    public void RejectsSeekWithoutKnownDuration()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot() with { Duration = TimeSpan.Zero });
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Seek, TimeSpan.FromSeconds(10));
            Assert.Empty(received);
        }
    }

    [Fact]
    public void RejectsCommandsWhoseButtonsAreDisabled()
    {
        var (controller, fake, received) = CreateController(PlayingSnapshot() with { CanPause = false, CanStop = false });
        using (controller)
        {
            fake.Raise(MediaTransportCommand.Pause);
            fake.Raise(MediaTransportCommand.Stop);
            Assert.Empty(received);
        }
    }

    [Fact]
    public void SuspensionStopsPublishingAndResumeRestoresLatestSnapshot()
    {
        var fake = new FakeMediaTransportService();
        using var controller = new MediaTransportController(fake, _ => { });
        controller.Initialize(new nint(1));
        MediaTransportSnapshot snapshot = PlayingSnapshot();
        controller.Publish(snapshot);
        int countBeforeSuspend = fake.Published.Count;

        controller.Suspend();
        controller.Publish(snapshot with { Position = TimeSpan.FromSeconds(30) });
        controller.Resume();

        Assert.True(fake.SuspendCalled);
        Assert.Equal(snapshot with { Position = TimeSpan.FromSeconds(30) }, fake.Published.Last());
        Assert.True(fake.Published.Count > countBeforeSuspend);
    }

    [Fact]
    public void DisposeUnsubscribesFromService()
    {
        var fake = new FakeMediaTransportService();
        var received = new List<MediaTransportCommandEventArgs>();
        var controller = new MediaTransportController(fake, received.Add);
        controller.Initialize(new nint(1));
        controller.Publish(PlayingSnapshot());
        controller.Dispose();

        fake.Raise(MediaTransportCommand.Play);

        Assert.Empty(received);
        Assert.True(fake.Disposed);
    }

    [Fact]
    public void InitializationFailureDoesNotEscapeController()
    {
        var fake = new FakeMediaTransportService { ThrowOnInitialize = true };
        using var controller = new MediaTransportController(fake, _ => { });

        bool available = controller.Initialize(new nint(1));

        Assert.False(available);
        Assert.False(controller.IsAvailable);
    }

    [Fact]
    public void InvalidPositionIsNormalizedBeforePublishing()
    {
        var fake = new FakeMediaTransportService();
        using var controller = new MediaTransportController(fake, _ => { });
        controller.Initialize(new nint(1));
        controller.Publish(PlayingSnapshot() with { Position = TimeSpan.FromMinutes(10) });

        Assert.Equal(TimeSpan.FromMinutes(2), fake.Published.Last().Position);
    }

    static (MediaTransportController Controller, FakeMediaTransportService Service, List<MediaTransportCommandEventArgs> Received)
        CreateController(MediaTransportSnapshot snapshot)
    {
        var fake = new FakeMediaTransportService();
        var received = new List<MediaTransportCommandEventArgs>();
        var controller = new MediaTransportController(fake, received.Add);
        controller.Initialize(new nint(1));
        controller.Publish(snapshot);
        return (controller, fake, received);
    }

    static MediaTransportSnapshot PlayingSnapshot() => new(
        IsEnabled: true,
        IsMediaLoaded: true,
        PlaybackStatus: MediaTransportPlaybackStatus.Playing,
        CanPlay: false,
        CanPause: true,
        CanStop: true,
        CanPrevious: true,
        CanNext: true,
        Metadata: new MediaTransportMetadata("Track", MediaTransportMediaType.Music),
        Duration: TimeSpan.FromMinutes(2),
        Position: TimeSpan.FromSeconds(10));
}

sealed class FakeMediaTransportService : IMediaTransportService
{
    public bool IsAvailable { get; private set; }
    public bool Disposed { get; private set; }
    public bool SuspendCalled { get; private set; }
    public bool ThrowOnInitialize { get; init; }
    public List<MediaTransportSnapshot> Published { get; } = [];

    public event EventHandler<MediaTransportCommandEventArgs>? CommandRequested;

    public void Initialize(nint windowHandle)
    {
        if (ThrowOnInitialize)
            throw new InvalidOperationException("test failure");

        IsAvailable = windowHandle != nint.Zero;
    }

    public void Publish(MediaTransportSnapshot snapshot) => Published.Add(snapshot);

    public void Suspend() => SuspendCalled = true;

    public void Raise(MediaTransportCommand command, TimeSpan? position = null) =>
        CommandRequested?.Invoke(this, new MediaTransportCommandEventArgs(command, position));

    public void Dispose()
    {
        Disposed = true;
        IsAvailable = false;
        CommandRequested = null;
    }
}
