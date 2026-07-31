using System.Drawing;
using System.Windows.Forms;

using MpvNet.Windows.WPF.MsgBox;
using MpvNet.Windows.WPF;

using static MpvNet.Windows.Native.WinApi;

namespace MpvNet.Windows.WinForms;

public partial class MainForm
{
    public MainForm()
    {
        InitializeComponent();

        Text = AppInfo.Product;
        UpdateDarkMode();

        try
        {
            Instance = this;

            Player.FileLoaded += Player_FileLoaded;
            Player.Pause += Player_Pause;
            Player.PlaylistPosChanged += Player_PlaylistPosChanged;
            Player.Seek += UpdateProgressBar;
            Player.Shutdown += Player_Shutdown;
            Player.VideoSizeChanged += Player_VideoSizeChanged;
            Player.ClientMessage += Player_ClientMessage;

            GuiCommand.Current.ScaleWindow += GuiCommand_ScaleWindow;
            GuiCommand.Current.MoveWindow += GuiCommand_MoveWindow;
            GuiCommand.Current.WindowScaleNet += GuiCommand_WindowScaleNet;
            GuiCommand.Current.ShowMenu += GuiCommand_ShowMenu;

            Player.Init(Handle, true);

            Player.ObserveProperty("window-maximized", PropChangeWindowMaximized); // bool methods not working correctly
            Player.ObserveProperty("window-minimized", PropChangeWindowMinimized); // bool methods not working correctly
            Player.ObserveProperty("cursor-autohide", PropChangeCursorAutohide);

            Player.ObservePropertyBool("border", PropChangeBorder);
            Player.ObservePropertyBool("fullscreen", PropChangeFullscreen);
            Player.ObservePropertyBool("keepaspect-window", value => Player.KeepaspectWindow = value);
            Player.ObservePropertyBool("ontop", PropChangeOnTop);
            Player.ObservePropertyBool("title-bar", PropChangeTitleBar);

            Player.ObservePropertyString("sid", PropChangeSid);
            Player.ObservePropertyString("aid", PropChangeAid);
            Player.ObservePropertyString("vid", PropChangeVid);

            Player.ObservePropertyString("title", PropChangeTitle);
            Player.ObservePropertyInt("edition", PropChangeEdition);
            Player.ObservePropertyDouble("window-scale", PropChangeWindowScale);

            CommandLine.ProcessCommandLineArgsPostInit();
            ApplyInterfaceLanguageFromAlang();
            CommandLine.ProcessCommandLineFiles();
            CommandLine.ProcessCommandLineArgsPostFile();

            _taskbarButtonCreatedMessage = RegisterWindowMessage("TaskbarButtonCreated");

            if (Player.Screen > -1)
            {
                int targetIndex = Player.Screen;
                Screen[] screens = Screen.AllScreens;

                if (targetIndex < 0)
                    targetIndex = 0;

                if (targetIndex > screens.Length - 1)
                    targetIndex = screens.Length - 1;

                Screen screen = screens[Array.IndexOf(screens, screens[targetIndex])];
                Rectangle target = screen.Bounds;
                Left = target.X + (target.Width - Width) / 2;
                Top = target.Y + (target.Height - Height) / 2;
            }

            if (!Player.Border)
                FormBorderStyle = FormBorderStyle.None;

            Point pos = App.Settings.WindowPosition;

            if ((pos.X != 0 || pos.Y != 0) && App.RememberWindowPosition)
            {
                Left = pos.X - Width / 2;
                Top = pos.Y - Height / 2;

                Point location = App.Settings.WindowLocation;

                if (location.X == -1) Left = pos.X;
                if (location.X ==  1) Left = pos.X - Width;
                if (location.Y == -1) Top = pos.Y;
                if (location.Y ==  1) Top = pos.Y - Height;
            }

            if (Player.WindowMaximized)
            {
                SetFormPosAndSize(true);
                WindowState = FormWindowState.Maximized;
            }

            if (Player.WindowMinimized)
            {
                SetFormPosAndSize(true);
                WindowState = FormWindowState.Minimized;
            }

            Shown += (_, _) =>
            {
                if (_componentBootstrapStarted)
                    return;

                _componentBootstrapStarted = true;
                Program.StartComponentBootstrap();
            };
        }
        catch (Exception ex)
        {
            Msg.ShowException(ex);
        }
    }
}
