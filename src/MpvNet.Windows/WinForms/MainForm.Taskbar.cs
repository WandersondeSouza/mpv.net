using MpvNet;
using MpvNet.Help;
using MpvNet.Windows.Services.MediaTransport;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    const int TaskbarThumbnailClicked = 0x1800;

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

    static TaskbarThumbnailButton[] BuildTaskbarThumbnailButtons(MediaTransportSnapshot snapshot)
    {
        bool isPlaying = snapshot.PlaybackStatus == MediaTransportPlaybackStatus.Playing;

        return new[]
        {
            new TaskbarThumbnailButton(
                TaskbarThumbnailButtonId.Previous,
                TaskbarThumbnailIcon.Previous,
                _("Previous File"),
                snapshot.CanPrevious),
            new TaskbarThumbnailButton(
                TaskbarThumbnailButtonId.PlayPause,
                isPlaying ? TaskbarThumbnailIcon.Pause : TaskbarThumbnailIcon.Play,
                _("Play/Pause"),
                snapshot.CanPlay || snapshot.CanPause),
            new TaskbarThumbnailButton(
                TaskbarThumbnailButtonId.Next,
                TaskbarThumbnailIcon.Next,
                _("Next File"),
                snapshot.CanNext),
            new TaskbarThumbnailButton(
                TaskbarThumbnailButtonId.Donation,
                TaskbarThumbnailIcon.Donation,
                _("Donation"),
                true),
        };
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

            case TaskbarThumbnailButtonId.Donation:
                ProcessHelp.ShellExecute(App.DonationUrl);
                break;
        }
    }

    void RequestTaskbarMediaTransportCommand(MediaTransportCommand command) =>
        _mediaTransport?.RequestCommand(new MediaTransportCommandEventArgs(command));
}
