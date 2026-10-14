using System;
using System.Linq;

using MpvNet.Windows.Native;
using MpvNet.Windows.Services.MediaTransport;
using Xunit;

public sealed class TaskbarThumbnailControlsTests
{
    static MediaTransportSnapshot Loaded(bool paused = false) => new(
        true, true, paused ? MediaTransportPlaybackStatus.Paused : MediaTransportPlaybackStatus.Playing,
        paused, !paused, true, false, false, new("Video", MediaTransportMediaType.Video),
        TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30));

    static TaskbarThumbnailButton[] Buttons(MediaTransportSnapshot snapshot, int chapters, bool fullscreen = false) =>
        TaskbarThumbnailControls.Build(snapshot, fullscreen, chapters, text => text);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    public void ChaptersAreIndependentOfPlaylistNavigation(int chapterCount, bool enabled)
    {
        TaskbarThumbnailButton[] buttons = Buttons(Loaded(), chapterCount);
        Assert.Equal(new[]
        {
            TaskbarThumbnailButtonId.Previous, TaskbarThumbnailButtonId.PreviousChapter,
            TaskbarThumbnailButtonId.PlayPause, TaskbarThumbnailButtonId.NextChapter,
            TaskbarThumbnailButtonId.Next, TaskbarThumbnailButtonId.Donation,
        }, buttons.Select(button => button.Id));
        Assert.Equal(enabled, buttons[1].IsEnabled);
        Assert.Equal(enabled, buttons[3].IsEnabled);
        Assert.False(buttons[0].IsEnabled);
        Assert.False(buttons[4].IsEnabled);
        Assert.True(buttons[2].IsEnabled);
        Assert.Equal(TaskbarThumbnailIcon.Pause, buttons[2].Icon);
        Assert.True(buttons[5].IsEnabled);

        buttons = Buttons(Loaded() with { CanPrevious = true, CanNext = true }, chapterCount);
        Assert.True(buttons[0].IsEnabled);
        Assert.True(buttons[4].IsEnabled);
        Assert.Equal(enabled, buttons[1].IsEnabled);
        Assert.Equal(enabled, buttons[3].IsEnabled);
    }

    [Fact]
    public void PausedChapterChangesUpdateTheCacheWithoutMovingButtons()
    {
        var cache = new TaskbarThumbnailButtonCache();
        TaskbarThumbnailButton[] withChapters = Buttons(Loaded(paused: true), 3);
        TaskbarThumbnailButton[] withoutChapters = Buttons(Loaded(paused: true), 0);
        Assert.Equal(TaskbarThumbnailIcon.Play, withChapters[2].Icon);
        Assert.Equal(withChapters.Select(button => button.Id), withoutChapters.Select(button => button.Id));
        cache.Remember(withChapters, false);
        Assert.False(cache.IsCurrent(withoutChapters, false));
        cache.Remember(withoutChapters, false);
        Assert.False(cache.IsCurrent(withChapters, false));
        Assert.True(withChapters[1].IsEnabled && withChapters[3].IsEnabled);
        Assert.False(withoutChapters[1].IsEnabled || withoutChapters[3].IsEnabled);
    }

    [Fact]
    public void NoMediaAndFullscreenDisableMediaButtonsEvenWithOldChapterCount()
    {
        foreach (TaskbarThumbnailButton[] buttons in new[]
        {
            Buttons(MediaTransportSnapshot.Disabled, 3),
            Buttons(Loaded(), 3, fullscreen: true),
            Buttons(Loaded() with { IsMediaLoaded = false }, 3),
            Buttons(Loaded() with { IsEnabled = false }, 3),
        })
        {
            Assert.False(buttons[1].IsEnabled);
            Assert.False(buttons[3].IsEnabled);
            Assert.True(buttons[5].IsEnabled);
        }
        Assert.All(Buttons(Loaded(), 3, fullscreen: true).Take(5), button => Assert.False(button.IsEnabled));
        Assert.All(Buttons(MediaTransportSnapshot.Disabled, 3).Take(5), button => Assert.False(button.IsEnabled));
    }

    [Fact]
    public void ChapterTooltipsUseTheCurrentTranslationAndIconsDifferFromFiles()
    {
        TaskbarThumbnailButton[] buttons = TaskbarThumbnailControls.Build(Loaded(), false, 1,
            text => text switch
            {
                "Previous Chapter" => "Capítulo anterior",
                "Next Chapter" => "Próximo capítulo",
                _ => text,
            });
        Assert.Equal("Capítulo anterior", buttons[1].Tooltip);
        Assert.Equal("Próximo capítulo", buttons[3].Tooltip);
        Assert.NotEqual(buttons[0].Icon, buttons[1].Icon);
        Assert.NotEqual(buttons[4].Icon, buttons[3].Icon);
    }

    [Theory]
    [InlineData(TaskbarThumbnailButtonId.PreviousChapter, "osd-msg add chapter -1")]
    [InlineData(TaskbarThumbnailButtonId.NextChapter, "osd-msg add chapter 1")]
    public void ChapterClicksUseOscCommandsAndRejectUnavailableOrStaleMedia(
        TaskbarThumbnailButtonId buttonId, string expected)
    {
        Assert.Equal(expected, TaskbarThumbnailControls.GetChapterCommand(buttonId, true, false, false, 1, 4, 4));
        Assert.Equal(expected, TaskbarThumbnailControls.GetChapterCommand(buttonId, true, false, false, 5, 4, 4));
        Assert.Null(TaskbarThumbnailControls.GetChapterCommand(buttonId, true, false, false, 0, 4, 4));
        Assert.Null(TaskbarThumbnailControls.GetChapterCommand(buttonId, false, false, false, 5, 4, 4));
        Assert.Null(TaskbarThumbnailControls.GetChapterCommand(buttonId, true, true, false, 5, 4, 4));
        Assert.Null(TaskbarThumbnailControls.GetChapterCommand(buttonId, true, false, true, 5, 4, 4));
        Assert.Null(TaskbarThumbnailControls.GetChapterCommand(buttonId, true, false, false, 5, 4, 5));
    }

    [Theory]
    [InlineData(TaskbarThumbnailButtonId.Previous)]
    [InlineData(TaskbarThumbnailButtonId.Next)]
    [InlineData(TaskbarThumbnailButtonId.PlayPause)]
    [InlineData(TaskbarThumbnailButtonId.Donation)]
    public void OtherButtonsCannotIssueChapterCommands(TaskbarThumbnailButtonId buttonId)
    {
        Assert.Null(TaskbarThumbnailControls.GetChapterCommand(buttonId, true, false, false, 3, 1, 1));
    }
}
