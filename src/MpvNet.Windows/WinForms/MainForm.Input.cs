using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

using MpvNet.Windows.UI;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    readonly VideoClickArbiter _videoClick = new(SystemInformation.DoubleClickTime);
    System.Windows.Forms.Timer? _videoClickTimer;
    long _scheduledVideoClick;
    bool _closingInput;
    Point _videoClickPosition;

    void BeginVideoClick()
    {
        _videoClickTimer?.Stop();
        _videoClickPosition = Cursor.Position;
        long generation = _videoClick.Begin(
            !_closingInput && _mediaTransportMediaLoaded && !ContextMenu.IsVisible && !IsMouseInOsc(),
            Environment.TickCount64);

        // Expansion happens in mpv when it resolves the active binding. Carry the
        // gesture ID through its event queue so a late message cannot pause new media.
        Player.SetPropertyString("user-data/mpvnet-click", generation.ToString(CultureInfo.InvariantCulture));
    }

    void CancelVideoClick()
    {
        _videoClick.Invalidate();
        _videoClickTimer?.Stop();
    }

    void ScheduleVideoClick(long generation)
    {
        if (_closingInput || _managedResourcesDisposed || !_mediaTransportMediaLoaded ||
            ContextMenu.IsVisible || !_videoClick.TrySchedule(generation, Environment.TickCount64, out int delay))
            return;

        if (_videoClickTimer == null)
        {
            _videoClickTimer = new System.Windows.Forms.Timer();
            _videoClickTimer.Tick += VideoClickTimer_Tick;
        }

        _scheduledVideoClick = generation;
        _videoClickTimer.Interval = delay;
        _videoClickTimer.Start();
    }

    void VideoClickTimer_Tick(object? sender, EventArgs e)
    {
        _videoClickTimer?.Stop();
        if (_closingInput || _managedResourcesDisposed || !_mediaTransportMediaLoaded ||
            ContextMenu.IsVisible || !_videoClick.TryComplete(_scheduledVideoClick))
            return;

        Command.Current.Commands["play-pause"](Array.Empty<string>());
    }

    void DisposeVideoClick()
    {
        _closingInput = true;
        CancelVideoClick();
        if (_videoClickTimer != null)
        {
            _videoClickTimer.Tick -= VideoClickTimer_Tick;
            _videoClickTimer.Dispose();
            _videoClickTimer = null;
        }
    }
}
