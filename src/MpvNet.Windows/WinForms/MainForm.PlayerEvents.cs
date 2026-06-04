using System.Drawing;
using System.Windows.Forms;

using MpvNet.Help;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    void Player_ClientMessage(string[] args)
    {
        if (Command.Current.Commands.ContainsKey(args[0]))
            Command.Current.Commands[args[0]].Invoke(new ArraySegment<string>(args, 1, args.Length - 1));
        else if (GuiCommand.Current.Commands.ContainsKey(args[0]))
            BeginInvoke(() => GuiCommand.Current.Commands[args[0]].Invoke(new ArraySegment<string>(args, 1, args.Length - 1)));
    }

    void Player_PlaylistPosChanged(int pos)
    {
        if (pos == -1)
            SetTitle();
    }

    void PropChangeWindowScale(double scale)
    {
        if (!WasShown)
            return;

        BeginInvoke(() => {
            SetSize(
                (int)(Player.VideoSize.Width * scale),
                (int)Math.Floor(Player.VideoSize.Height * scale),
                Screen.FromControl(this), false);
        });
    }

    void Player_Shutdown() => BeginInvoke(Close);

    void Player_VideoSizeChanged(Size value) => BeginInvoke(() =>
    {
        if (!KeepSize())
            SetFormPosAndSize();
    });

    void Player_FileLoaded()
    {
        NormalizeLoadedMediaTitle();
        TaskHelp.Run(() => {
            Player.UpdateTracks();
        });

        BeginInvoke(() => {
            SetTitleInternal();

            int interval = (int)(Player.Duration.TotalMilliseconds / 100);

            if (interval < 100)
                interval = 100;

            if (interval > 1000)
                interval = 1000;

            ProgressTimer.Interval = interval;
            UpdateProgressBar();
        });

        string path = Player.GetPropertyString("path");

        path = MainPlayer.ConvertFilePath(path);

        if (path.Contains("://"))
        {
            string title = Player.GetPropertyString("media-title");

            if (!string.IsNullOrEmpty(title) && path != title)
                path = path + "|" + title;
        }

        if (!string.IsNullOrEmpty(path) && path != "-" && path != @"bd://" && path != @"dvd://")
        {
            if (App.Settings.RecentFiles.Contains(path))
                App.Settings.RecentFiles.Remove(path);

            App.Settings.RecentFiles.Insert(0, path);

            while (App.Settings.RecentFiles.Count > App.RecentCount)
                App.Settings.RecentFiles.RemoveAt(App.RecentCount);
        }
    }

    void Player_Pause()
    {
        if (_taskbar != null && Player.TaskbarProgress)
            _taskbar.SetState(Player.Paused ? TaskbarStates.Paused : TaskbarStates.Normal);
    }
}
