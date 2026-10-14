using MpvNet.Windows.Services.MediaTransport;

namespace MpvNet.Windows.Native;

public static class TaskbarThumbnailControls
{
    public static TaskbarThumbnailButton[] Build(
        MediaTransportSnapshot snapshot, bool fullscreen, int chapterCount, Func<string, string> translate)
    {
        if (fullscreen)
            snapshot = MediaTransportSnapshot.Disabled;

        bool hasChapters = snapshot.IsEnabled && snapshot.IsMediaLoaded && chapterCount > 0;
        bool isPlaying = snapshot.PlaybackStatus == MediaTransportPlaybackStatus.Playing;

        return
        [
            new(TaskbarThumbnailButtonId.Previous, TaskbarThumbnailIcon.Previous,
                translate("Previous File"), snapshot.CanPrevious),
            new(TaskbarThumbnailButtonId.PreviousChapter, TaskbarThumbnailIcon.PreviousChapter,
                translate("Previous Chapter"), hasChapters),
            new(TaskbarThumbnailButtonId.PlayPause, isPlaying ? TaskbarThumbnailIcon.Pause : TaskbarThumbnailIcon.Play,
                translate("Play/Pause"), snapshot.CanPlay || snapshot.CanPause),
            new(TaskbarThumbnailButtonId.NextChapter, TaskbarThumbnailIcon.NextChapter,
                translate("Next Chapter"), hasChapters),
            new(TaskbarThumbnailButtonId.Next, TaskbarThumbnailIcon.Next,
                translate("Next File"), snapshot.CanNext),
            new(TaskbarThumbnailButtonId.Donation, TaskbarThumbnailIcon.Donation,
                translate("Help the player"), true),
        ];
    }

    public static string? GetChapterCommand(
        TaskbarThumbnailButtonId buttonId, bool mediaLoaded, bool fullscreen, bool closing,
        int chapterCount, long displayedGeneration, long currentGeneration)
    {
        if (!mediaLoaded || fullscreen || closing || chapterCount <= 0 || displayedGeneration != currentGeneration)
            return null;

        // Match the OSC's default left-click commands, including mpv's handling of chapter boundaries.
        return buttonId switch
        {
            TaskbarThumbnailButtonId.PreviousChapter => "osd-msg add chapter -1",
            TaskbarThumbnailButtonId.NextChapter => "osd-msg add chapter 1",
            _ => null,
        };
    }
}
