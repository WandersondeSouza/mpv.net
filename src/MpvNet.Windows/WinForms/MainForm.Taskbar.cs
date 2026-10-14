using MpvNet;
using MpvNet.Help;
using MpvNet.Windows.Native;
using MpvNet.Windows.Services.MediaTransport;
using System.Threading;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    const int TaskbarThumbnailClicked = 0x1800;
    long _taskbarMediaGeneration = -1;

    static bool TryGetTaskbarThumbnailButtonId(IntPtr wParam, out TaskbarThumbnailButtonId buttonId)
    {
        uint command = unchecked((uint)wParam.ToInt64());
        if ((command >> 16) != TaskbarThumbnailClicked)
        {
            buttonId = default;
            return false;
        }

        buttonId = (TaskbarThumbnailButtonId)(command & 0xffff);
        return Enum.IsDefined(buttonId);
    }

    void UpdateTaskbarThumbnail(MediaTransportSnapshot snapshot)
    {
        _taskbar?.UpdateThumbnailButtons(BuildTaskbarThumbnailButtons(snapshot));
    }

    TaskbarThumbnailButton[] BuildTaskbarThumbnailButtons(MediaTransportSnapshot snapshot)
    {
        long generation = Interlocked.Read(ref _mediaTransportGeneration);
        if (_managedResourcesDisposed || _closingInput || !_mediaTransportMediaLoaded)
            snapshot = MediaTransportSnapshot.Disabled;

        int chapterCount = snapshot.IsEnabled && snapshot.IsMediaLoaded
            ? Player.GetPropertyInt("chapter-list/count") : 0;
        if (generation != Interlocked.Read(ref _mediaTransportGeneration) || !_mediaTransportMediaLoaded)
            snapshot = MediaTransportSnapshot.Disabled;

        _taskbarMediaGeneration = generation;
        return TaskbarThumbnailControls.Build(snapshot, IsFullscreen, chapterCount, text => _(text));
    }

    void WpfTranslator_LanguageChanged(object? sender, System.Globalization.CultureInfo culture)
    {
        if (_taskbar != null)
            _taskbar.RefreshThumbnailButtons(BuildTaskbarThumbnailButtons(_mediaTransport?.Snapshot ?? MediaTransportSnapshot.Disabled));
    }

    void HandleTaskbarThumbnailButton(TaskbarThumbnailButtonId buttonId)
    {
        Log.Debug($"Taskbar thumbnail button clicked. button={buttonId}");

        switch (buttonId)
        {
            case TaskbarThumbnailButtonId.Previous:
                RequestTaskbarMediaTransportCommand(MediaTransportCommand.Previous);
                break;

            case TaskbarThumbnailButtonId.PlayPause:
                MediaTransportSnapshot snapshot = _mediaTransport?.Snapshot ?? MediaTransportSnapshot.Disabled;
                if (snapshot.IsEnabled && snapshot.IsMediaLoaded && (snapshot.CanPlay || snapshot.CanPause))
                {
                    Log.Debug("Taskbar Play/Pause requested. command='cycle pause'");
                    Player.Command("cycle pause");
                }
                break;

            case TaskbarThumbnailButtonId.Next:
                RequestTaskbarMediaTransportCommand(MediaTransportCommand.Next);
                break;

            case TaskbarThumbnailButtonId.PreviousChapter:
            case TaskbarThumbnailButtonId.NextChapter:
                RequestTaskbarChapterCommand(buttonId);
                break;

            case TaskbarThumbnailButtonId.Donation:
                ProcessHelp.ShellExecute(App.DonationUrl);
                break;
        }
    }

    void RequestTaskbarMediaTransportCommand(MediaTransportCommand command) =>
        _mediaTransport?.RequestCommand(new MediaTransportCommandEventArgs(command));

    void RequestTaskbarChapterCommand(TaskbarThumbnailButtonId buttonId)
    {
        if (_managedResourcesDisposed || _closingInput || !_mediaTransportMediaLoaded || IsFullscreen)
            return;

        long displayedGeneration = _taskbarMediaGeneration;
        int chapterCount = Player.GetPropertyInt("chapter-list/count");
        string? command = TaskbarThumbnailControls.GetChapterCommand(
            buttonId, _mediaTransportMediaLoaded, IsFullscreen, _managedResourcesDisposed || _closingInput,
            chapterCount, displayedGeneration, Interlocked.Read(ref _mediaTransportGeneration));
        if (command != null)
            Player.Command(command);
    }
}
