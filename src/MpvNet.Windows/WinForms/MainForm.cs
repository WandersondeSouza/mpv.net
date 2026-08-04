
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Text.RegularExpressions;

using MpvNet.Windows.WPF;
using MpvNet.Windows.UI;
using MpvNet.Help;
using MpvNet.Extensions;
using MpvNet.MVVM;
using MpvNet.Windows.WPF.MsgBox;

using WpfControls = System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;

using static MpvNet.Windows.Native.WinApi;
using static MpvNet.Windows.Help.WinApiHelp;

namespace MpvNet.Windows.WinForms;

public partial class MainForm : Form
{
    public SnapManager SnapManager = new SnapManager();
    public IntPtr MpvWindowHandle { get; set; }
    public bool WasShown { get; set; }
    public static MainForm? Instance { get; set; }
    new WpfControls.ContextMenu ContextMenu { get; } = new WpfControls.ContextMenu();
    AutoResetEvent MenuAutoResetEvent { get; } = new AutoResetEvent(false);
    Point _lastCursorPosition;
    Taskbar? _taskbar;
    Point _mouseDownLocation;
    List<Binding>? _confBindings;

    int _lastCursorChanged;
    int _lastCycleFullscreen;
    int _taskbarButtonCreatedMessage;
    int _cursorAutohide = 1000;

    bool _contextMenuIsReady;
    bool _contextMenuClosedHandlerAttached;
    bool _managedResourcesDisposed;
    bool _wasMaximized;
    bool _maxSizeSet;
    bool _isCursorVisible = true;
    bool _componentBootstrapStarted;

    void UpdateDarkMode()
    {
        if (Environment.OSVersion.Version >= new Version(10, 0, 18985))
            DwmSetWindowAttribute(Handle, 20, new[] { Theme.DarkMode ? 1 : 0 }, 4);  // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
    }

    bool KeepSize() => App.StartSize == "session" || App.StartSize == "always";

    void UpdateMenu()
    {
        Player.UpdateExternalTracks();

        lock (Player.MediaTracksLock)
        {
            var trackMenuItem = FindMenuItem(_("Track"), "Track");

            if (trackMenuItem != null)
            {
                trackMenuItem.Items.Clear();

                var audTracks = Player.MediaTracks.Where(track => track.Type == "a");
                var subTracks = Player.MediaTracks.Where(track => track.Type == "s");
                var vidTracks = Player.MediaTracks.Where(track => track.Type == "v");
                var ediTracks = Player.MediaTracks.Where(track => track.Type == "e");

                AddTrackMenuItems(trackMenuItem, vidTracks, "vid", Player.VID);

                if (vidTracks.Any())
                    trackMenuItem.Items.Add(new WpfControls.Separator());

                AddTrackMenuItems(trackMenuItem, audTracks, "aid", Player.AID);

                if (subTracks.Any())
                    trackMenuItem.Items.Add(new WpfControls.Separator());

                AddTrackMenuItems(trackMenuItem, subTracks, "sid", Player.SID);

                if (subTracks.Any())
                    AddNoSubtitlesMenuItem(trackMenuItem);

                if (ediTracks.Any())
                    trackMenuItem.Items.Add(new WpfControls.Separator());

                AddEditionMenuItems(trackMenuItem, ediTracks);
            }
        }

        var chaptersMenuItem = FindMenuItem(_("Chapter"), "Chapters");

        if (chaptersMenuItem != null)
        {
            chaptersMenuItem.Items.Clear();

            foreach (Chapter chapter in Player.GetChapters())
            {
                var menuItem = new WpfControls.MenuItem
                {
                    Header = chapter.Title,
                    InputGestureText = chapter.TimeDisplay
                };
                
                menuItem.Click += (sender, args) =>
                    Player.CommandV("seek", chapter.Time.ToString(CultureInfo.InvariantCulture), "absolute");

                chaptersMenuItem.Items.Add(menuItem);
            }
        }

        var recentMenuItem = FindMenuItem(_("Recent Files"), "Recent");

        if (recentMenuItem != null)
        {
            recentMenuItem.Items.Clear();

            foreach (string path in App.Settings.RecentFiles)
            {
                var file = AppClass.GetTitleAndPath(path);
                var menuItem = MenuHelp.Add(recentMenuItem.Items, file.Title.ShortPath(100));

                if (menuItem != null)
                    menuItem.Click += (sender, args) => Player.LoadFiles(new[] { file.Path }, true, false);
            }

            recentMenuItem.Items.Add(new WpfControls.Separator());
            var clearMenuItem = new WpfControls.MenuItem() { Header = _("Clear List") };
            clearMenuItem.Click += (sender, args) => App.Settings.RecentFiles.Clear();
            recentMenuItem.Items.Add(clearMenuItem);
        }

        var titlesMenuItem = FindMenuItem(_("Title"), "Titles");

        if (titlesMenuItem != null)
        {
            titlesMenuItem.Items.Clear();

            lock (Player.BluRayTitles)
            {
                List<(int Index, TimeSpan Length)> items = new List<(int, TimeSpan)>(); 

                for (int i = 0; i < Player.BluRayTitles.Count; i++)
                    items.Add((i, Player.BluRayTitles[i]));

                var titleItems = items.OrderByDescending(item => item.Length)
                                      .Take(20)
                                      .OrderBy(item => item.Index);

                foreach (var item in titleItems)
                {
                    if (item.Length != TimeSpan.Zero)
                    {
                        var menuItem = MenuHelp.Add(titlesMenuItem.Items, $"Title {item.Index + 1}");

                        if (menuItem != null)
                        {
                            menuItem.InputGestureText = item.Length.ToString();
                            menuItem.Click += (sender, args) => Player.SetBluRayTitle(item.Index);
                        }
                    }
                }
            }
        }

        var profilesMenuItem = FindMenuItem(_("Profile"), "Profile");

        if (profilesMenuItem != null && !profilesMenuItem.HasItems)
        {
            foreach (string profile in Player.ProfileNames)
            {
                if (!profile.StartsWith("extension."))
                {
                    var menuItem = MenuHelp.Add(profilesMenuItem.Items, profile);

                    if (menuItem != null)
                    {
                        menuItem.Click += (sender, args) =>
                        {
                            Player.CommandV("show-text", profile);
                            Player.CommandV("apply-profile", profile);
                        };
                    }
                }
            }

            profilesMenuItem.Items.Add(new WpfControls.Separator());
            var showProfilesMenuItem = new WpfControls.MenuItem() { Header = _("Show Profiles") };
            showProfilesMenuItem.Click += (sender, args) => Player.Command("script-message-to mpvnet show-profiles");
            profilesMenuItem.Items.Add(showProfilesMenuItem);
        }

        var audioDevicesMenuItem = FindMenuItem(_("Audio Device"), "Audio Device");

        if (audioDevicesMenuItem != null)
        {
            audioDevicesMenuItem.Items.Clear();

            foreach (var pair in Player.AudioDevices)
            {
                var menuItem = MenuHelp.Add(audioDevicesMenuItem.Items, pair.Value);

                if (menuItem != null)
                {
                    menuItem.IsChecked = pair.Name == Player.GetPropertyString("audio-device");

                    menuItem.Click += (sender, args) =>
                    {
                        Player.SetPropertyString("audio-device", pair.Name);
                        Player.CommandV("show-text", pair.Value);
                        App.Settings.AudioDevice = pair.Name;
                    };
                }
            }
        }

        var customMenuItem = FindMenuItem(_("Custom"), "Custom");

        if (customMenuItem != null && !customMenuItem.HasItems)
        {
            var customBindings = _confBindings!.Where(it => it.IsCustomMenu);

            if (customBindings.Any())
            {
                foreach (Binding binding in customBindings)
                {
                    var menuItem = MenuHelp.Add(customMenuItem.Items, binding.Comment);

                    if (menuItem != null)
                    {
                        menuItem.Click += (sender, args) => Player.Command(binding.Command);
                        menuItem.InputGestureText = binding.Input;
                    }
                }
            }
            else
            {
                if (ContextMenu.Items.Contains(customMenuItem))
                    ContextMenu.Items.Remove(customMenuItem);
            }
        }
    }

    void ApplyInterfaceLanguageFromAlang()
    {
        string alang = Player.GetPropertyString("alang");

        if (string.IsNullOrWhiteSpace(alang))
            return;

        string language = WpfTranslator.GetEffectiveLanguageFromAlang(alang);

        if (App.Language == language)
            return;

        App.Language = language;
        TranslationProvider.Current?.Gettext("");
        Player.SetPropertyString("user-data/mpvnet-donation-url", AppClass.GetDonationUrl(App.Language));
        Player.SetPropertyString("osd-msg1", "${?playlist-playing-pos==-1:" + _("Drop files or URLs to play here.") + "}");
        App.EnsureInitialSelectMenuConf();
        RebuildContextMenu();
    }

    void SetFormPosAndSize(bool force = false, bool checkAutofit = true, bool load = false)
    {
        if (!force)
        {
            if (WindowState != FormWindowState.Normal)
                return;

            if (Player.Fullscreen)
            {
                CycleFullscreen(true);
                return;
            }
        }

        Screen screen = Screen.FromControl(this);
        Rectangle workingArea = GetWorkingArea(Handle, screen.WorkingArea);
        int autoFitHeight = Convert.ToInt32(workingArea.Height * Player.Autofit);

        if (App.AutofitAudio > 1)
            App.AutofitAudio = 1;

        if (App.AutofitImage > 1)
            App.AutofitImage = 1;

        bool isAudio = FileTypes.IsAudio(Player.Path.Ext());
        
        if (isAudio)
            autoFitHeight = Convert.ToInt32(workingArea.Height * App.AutofitAudio);

        if (FileTypes.IsImage(Player.Path.Ext()))
            autoFitHeight = Convert.ToInt32(workingArea.Height * App.AutofitImage);

        if (Player.VideoSize.Height == 0 || Player.VideoSize.Width == 0)
            Player.VideoSize = new Size((int)(autoFitHeight * (16 / 9f)), autoFitHeight);

        float minAspectRatio = isAudio ? App.MinimumAspectRatioAudio : App.MinimumAspectRatio;

        if (minAspectRatio != 0 && Player.VideoSize.Width / (float)Player.VideoSize.Height < minAspectRatio)
            Player.VideoSize = new Size((int)(autoFitHeight * minAspectRatio), autoFitHeight);

        Size videoSize = Player.VideoSize;

        int height = videoSize.Height;
        int width  = videoSize.Width;

        if (App.StartSize == "previous")
            App.StartSize = "height-session";

        if (Player.WasInitialSizeSet)
        {
            if (KeepSize())
            {
                width = ClientSize.Width;
                height = ClientSize.Height;
            }
            else if (App.StartSize == "height-always" || App.StartSize == "height-session")
            {
                height = ClientSize.Height;
                width = (int)Math.Ceiling(height * videoSize.Width / (double)videoSize.Height);
            }
            else if (App.StartSize == "width-always" || App.StartSize == "width-session")
            {
                width = ClientSize.Width;
                height = (int)Math.Floor(width * videoSize.Height / (double)videoSize.Width);
            }
        }
        else
        {
            Size windowSize = App.Settings.WindowSize;

            if (App.StartSize == "height-always" && windowSize.Height != 0)
            {
                height = windowSize.Height;
                width = (int)Math.Ceiling(height * videoSize.Width / (double)videoSize.Height);
            }
            else if (App.StartSize == "height-session" || App.StartSize == "session")
            {
                height = autoFitHeight;
                width = (int)Math.Ceiling(height * videoSize.Width / (double)videoSize.Height);
            }
            else if(App.StartSize == "width-always" && windowSize.Height != 0)
            {
                width = windowSize.Width;
                height = (int)Math.Floor(width * videoSize.Height / (double)videoSize.Width);
            }
            else if (App.StartSize == "width-session")
            {
                width = autoFitHeight / 9 * 16;
                height = (int)Math.Floor(width * videoSize.Height / (double)videoSize.Width);
            }
            else if (App.StartSize == "always" && windowSize.Height != 0)
            {
                height = windowSize.Height;
                width = windowSize.Width;
            }
            
            Player.WasInitialSizeSet = true;
        }

        SetSize(width, height, screen, checkAutofit, load);
    }

    void SetSize(int width, int height, Screen screen, bool checkAutofit = true, bool load = false)
    {
        Rectangle workingArea = GetWorkingArea(Handle, screen.WorkingArea);

        int maxHeight = workingArea.Height - (Height - ClientSize.Height) - 2;
        int maxWidth = workingArea.Width - (Width - ClientSize.Width);

        int startWidth = width;
        int startHeight = height;

        if (checkAutofit)
        {
            if (height < maxHeight * Player.AutofitSmaller)
            {
                height = (int)(maxHeight * Player.AutofitSmaller);
                width = (int)Math.Ceiling(height * startWidth / (double)startHeight);
            }

            if (height > maxHeight * Player.AutofitLarger)
            {
                height = (int)(maxHeight * Player.AutofitLarger);
                width = (int)Math.Ceiling(height * startWidth / (double)startHeight);
            }
        }

        if (width > maxWidth)
        {
            width = maxWidth;
            height = (int)Math.Floor(width * startHeight / (double)startWidth);
        }

        if (height > maxHeight)
        {
            height = maxHeight;
            width = (int)Math.Ceiling(height * startWidth / (double)startHeight);
        }

        if (height < maxHeight * 0.1)
        {
            height = (int)(maxHeight * 0.1);
            width = (int)Math.Ceiling(height * startWidth / (double)startHeight);
        }

        Point middlePos = new Point(Left + Width / 2, Top + Height / 2);
        var rect = new RECT(new Rectangle(screen.Bounds.X, screen.Bounds.Y, width, height));

        AddWindowBorders(Handle, ref rect, GetDpi(Handle), !Player.TitleBar);

        width = rect.Width;
        height = rect.Height;

        int left = Convert.ToInt32(middlePos.X - width / 2.0);
        int top = Convert.ToInt32(middlePos.Y - height / 2.0);

        if (!Player.TitleBar)
            top -= Convert.ToInt32(GetTitleBarHeight(Handle, GetDpi(Handle)) / 2.0);

        Rectangle currentRect = new Rectangle(Left, Top, Width, Height);

        if (GetHorizontalLocation(screen) == -1) left = Left;
        if (GetHorizontalLocation(screen) ==  1) left = currentRect.Right - width;

        if (GetVerticalLocation(screen) == -1) top = Top;
        if (GetVerticalLocation(screen) ==  1) top = currentRect.Bottom - height;

        Screen[] screens = Screen.AllScreens;

        int minLeft   = screens.Select(val => GetWorkingArea(Handle, val.WorkingArea).X).Min();
        int maxRight  = screens.Select(val => GetWorkingArea(Handle, val.WorkingArea).Right).Max();
        int minTop    = screens.Select(val => GetWorkingArea(Handle, val.WorkingArea).Y).Min();
        int maxBottom = screens.Select(val => GetWorkingArea(Handle, val.WorkingArea).Bottom).Max();

        if (load)
        {
            string geometryString = Player.GetPropertyString("geometry");

            if (!string.IsNullOrEmpty(geometryString))
            {
                var pos = ParseGeometry(geometryString, width, height);

                if (pos.X != int.MaxValue)
                    left = pos.X;

                if (pos.Y != int.MaxValue)
                    top = pos.Y;
            }
        }

        if (left < minLeft)
            left = minLeft;

        if (left + width > maxRight)
            left = maxRight - width;

        if (top < minTop)
            top = minTop;

        if (top + height > maxBottom)
            top = maxBottom - height;

        uint SWP_NOACTIVATE = 0x0010;
        SetWindowPos(Handle, IntPtr.Zero, left, top, width, height, SWP_NOACTIVATE);
    }

    Point ParseGeometry(string input, int width, int height)
    {
        int x = int.MaxValue;
        int y = int.MaxValue;

        Match match = Regex.Match(input, @"^\+(\d+)%?\+(\d+)%?$");

        if (match.Success)
        {
            Rectangle workingArea = GetWorkingArea(Handle, Screen.FromHandle(Handle).WorkingArea);

            x = int.Parse(match.Groups[1].Value);
            y = int.Parse(match.Groups[2].Value);
            
            x = workingArea.Left + Convert.ToInt32((workingArea.Width - width) / 100.0 * x);
            y = workingArea.Top + Convert.ToInt32((workingArea.Height - height) / 100.0 * y);
        }

        return new Point(x, y);
    }

    public int GetHorizontalLocation(Screen screen)
    {
        Rectangle workingArea = GetWorkingArea(Handle, screen.WorkingArea);
        Rectangle rect = new Rectangle(Left - workingArea.X, Top - workingArea.Y, Width, Height);

        if (workingArea.Width / (float)Width < 1.1)
            return 0;

        if (rect.X * 3 < workingArea.Width - rect.Right)
            return -1;

        if (rect.X > (workingArea.Width - rect.Right) * 3)
            return 1;

        return 0;
    }

    public int GetVerticalLocation(Screen screen)
    {
        Rectangle workingArea = GetWorkingArea(Handle, screen.WorkingArea);
        Rectangle rect = new Rectangle(Left - workingArea.X, Top - workingArea.Y, Width, Height);

        if (workingArea.Height / (float)Height < 1.1)
            return 0;

        if (rect.Y * 3 < workingArea.Height - rect.Bottom)
            return -1;

        if (rect.Y > (workingArea.Height - rect.Bottom) * 3)
            return 1;

        return 0;
    }

    public void InitAndBuildContextMenu()
    {
        if (!_contextMenuClosedHandlerAttached)
        {
            ContextMenu.Closed += ContextMenu_Closed;
            _contextMenuClosedHandlerAttached = true;
        }

        _contextMenuIsReady = false;
        ContextMenu.Items.Clear();
        ContextMenu.UseLayoutRounding = true;

        var (menuBindings, confBindings) = App.InputConf.GetBindings();
        _confBindings = confBindings;
        var activeBindings = InputHelp.GetActiveBindings(menuBindings);
        var defaultMenuLabels = GetDefaultMenuLabels();

        foreach (Binding binding in menuBindings)
        {
            Binding tempBinding = binding;

            if (!binding.IsMenu)
                continue;

            string menuPath = GetLocalizedMenuPath(tempBinding, defaultMenuLabels);
            var menuItem = MenuHelp.Add(ContextMenu.Items, menuPath);

            if (menuItem != null)
            {
                menuItem.Click += (sender, args) => {
                    try {
                        BackgroundTaskRunner.Run(() => {
                            MenuAutoResetEvent.WaitOne();
                            System.Windows.Application.Current.Dispatcher.Invoke(
                                DispatcherPriority.Background, new Action(delegate { }));
                            if (!string.IsNullOrEmpty(tempBinding.Command))
                                Player.Command(tempBinding.Command);                
                        });
                    }
                    catch (Exception ex) {
                        Msg.ShowException(ex);
                    }
                };

                menuItem.InputGestureText = InputHelp.GetBindingsForCommand(activeBindings, tempBinding.Command);
            }
        }

        _contextMenuIsReady = true;
    }

    public void RebuildContextMenu()
    {
        if (!_contextMenuIsReady)
            return;

        bool wasOpen = ContextMenu.IsOpen;

        if (wasOpen)
            ContextMenu.IsOpen = false;

        InitAndBuildContextMenu();

        if (wasOpen)
        {
            UpdateMenu();
            ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            ContextMenu.IsOpen = true;
        }
    }
    
    void SetTitle() => BeginInvoke(SetTitleInternal);

    void SetTitleInternal()
    {
        string? title = Title;

        if (title == "${filename}" && Player.Path.ContainsEx("://"))
            title = "${media-title}";

        string text = Player.Expand(title);

        if (text == "(unavailable)" || Player.PlaylistPos == -1)
            text = AppInfo.Product;
        else
            text = TitleHelp.NormalizeMediaTitle(text);

        Text = text;
    }

    void NormalizeLoadedMediaTitle()
    {
        if (CommandLine.Contains("force-media-title"))
            return;

        string title = Player.GetPropertyString("media-title");
        string normalizedTitle = TitleHelp.NormalizeMediaTitle(title);

        if (!string.IsNullOrEmpty(normalizedTitle) && normalizedTitle != title)
            Player.SetPropertyString("file-local-options/force-media-title", normalizedTitle);
    }

    void SaveWindowProperties()
    {
        if (WindowState == FormWindowState.Normal && WasShown)
        {
            SavePosition();
            App.Settings.WindowSize = ClientSize;
        }
    }

    void SavePosition()
    {
        Point pos = new Point(Left + Width / 2, Top + Height / 2);
        Screen screen = Screen.FromControl(this);

        int x = GetHorizontalLocation(screen);
        int y = GetVerticalLocation(screen);

        if (x == -1) pos.X = Left;
        if (x ==  1) pos.X = Left + Width;
        if (y == -1) pos.Y = Top;
        if (y ==  1) pos.Y = Top + Height;

        App.Settings.WindowPosition = pos;
        App.Settings.WindowLocation = new Point(x, y);
    }

    protected override CreateParams CreateParams {
        get {
            CreateParams cp = base.CreateParams;
            cp.Style |= 0x00020000 /* WS_MINIMIZEBOX */;
            return cp;
        }
    }

    string? _title;

    public string? Title {
        get => _title;
        set {
            if (string.IsNullOrEmpty(value))
                return;

            if (value.EndsWith("} - mpv"))
                value = value[..^6];
            else if (value.EndsWith("} - mpv.net"))
                value = value[..^10];

            _title = value;
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case 0x0007: // WM_SETFOCUS
            case 0x0008: // WM_KILLFOCUS
            case 0x0021: // WM_MOUSEACTIVATE
            case 0x0100: // WM_KEYDOWN
            case 0x0101: // WM_KEYUP
            case 0x0104: // WM_SYSKEYDOWN
            case 0x0105: // WM_SYSKEYUP
            case 0x0201: // WM_LBUTTONDOWN
            case 0x0202: // WM_LBUTTONUP
            case 0x0204: // WM_RBUTTONDOWN
            case 0x0205: // WM_RBUTTONUP
            case 0x0206: // WM_RBUTTONDBLCLK
            case 0x0207: // WM_MBUTTONDOWN
            case 0x0208: // WM_MBUTTONUP
            case 0x0209: // WM_MBUTTONDBLCLK
            case 0x020a: // WM_MOUSEWHEEL
            case 0x020b: // WM_XBUTTONDOWN
            case 0x020c: // WM_XBUTTONUP
            case 0x020e: // WM_MOUSEHWHEEL
            case 0x0280: // WM_IME_REPORT
            case 0x0281: // WM_IME_SETCONTEXT
            case 0x0282: // WM_IME_NOTIFY
            case 0x0283: // WM_IME_CONTROL
            case 0x0284: // WM_IME_COMPOSITIONFULL
            case 0x0285: // WM_IME_SELECT
            case 0x0286: // WM_IME_CHAR
            case 0x0288: // WM_IME_REQUEST
            case 0x0290: // WM_IME_KEYDOWN
            case 0x0291: // WM_IME_KEYUP
            case 0x02a3: // WM_MOUSELEAVE
                {
                    bool ignore = false;

                    if (m.Msg == 0x0100) // WM_KEYDOWN
                    {
                        Keys keyCode = (Keys)(int)m.WParam & Keys.KeyCode;

                        if (keyCode == Keys.Escape && _contextMenuIsReady && ContextMenu.IsOpen)
                        {
                            ignore = true;
                            ContextMenu.IsOpen = false;
                        }
                        else if (keyCode == Keys.Escape && (IsFullscreen || Player.Fullscreen))
                        {
                            ignore = true;
                            CycleFullscreen(false);
                        }
                    }

                    if (MpvWindowHandle == IntPtr.Zero)
                        MpvWindowHandle = FindWindowEx(Handle, IntPtr.Zero, "mpv", null);

                    if (MpvWindowHandle != IntPtr.Zero && !ignore)
                        m.Result = SendMessage(MpvWindowHandle, m.Msg, m.WParam, m.LParam);
                }
                break;
            case 0x001A: // WM_SETTINGCHANGE
                UpdateDarkMode();
                break;
            case 0x51: // WM_INPUTLANGCHANGE
                ActivateKeyboardLayout(m.LParam, 0x00000100u /*KLF_SETFORPROCESS*/);
                break;
            case 0x319: // WM_APPCOMMAND
                {
                    string? key = MpvHelp.WM_APPCOMMAND_to_mpv_key((int)(m.LParam.ToInt64() >> 16 & ~0xf000));
                    bool inputMediaKeys = Player.GetPropertyBool("input-media-keys");

                    if (key != null && inputMediaKeys)
                    {
                        Player.Command("keypress " + key);
                        m.Result = new IntPtr(1);
                        return;
                    }
                }
                break;
            case 0x312: // WM_HOTKEY
                GlobalHotkey.Execute(m.WParam.ToInt32());
                break;
            case 0x200: // WM_MOUSEMOVE
                if (Environment.TickCount - _lastCycleFullscreen > 500)
                {
                    Point pos = PointToClient(Cursor.Position);
                    Player.Command($"mouse {pos.X} {pos.Y}");
                }

                if (IsCursorPosDifferent(_lastCursorPosition))
                    ShowCursor();
                break;
            case 0x203: // WM_LBUTTONDBLCLK
                {
                    Point pos = PointToClient(Cursor.Position);
                    Player.Command($"mouse {pos.X} {pos.Y} 0 double");
                }
                break;
            case 0x2E0: // WM_DPICHANGED
                {
                    if (!WasShown)
                        break;

                    RECT rect = Marshal.PtrToStructure<RECT>(m.LParam);
                    SetWindowPos(Handle, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height, 0);
                }
                break;
            case 0x0112: // WM_SYSCOMMAND
                {
                    // with title-bar=no when the window is restored from minimizing the height is too high  
                    if (!Player.TitleBar)
                    {
                        int SC_MINIMIZE = 0xF020;

                        if (m.WParam == (nint)SC_MINIMIZE)
                        {
                            MaximumSize = Size;
                            _maxSizeSet = true;
                        }
                    }
                }
                break;
            case 0x0083: // WM_NCCALCSIZE
                if ((int)m.WParam == 1 && !Player.TitleBar && !IsFullscreen)
                {
                    var nccalcsize_params = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(m.LParam);
                    RECT[] rects = nccalcsize_params.rgrc;
                    int h = GetTitleBarHeight(Handle, GetDpi(Handle));
                    rects[0].Top = rects[0].Top - h;
                    Marshal.StructureToPtr(nccalcsize_params, m.LParam, false);
                }
                break;
            case 0x231: // WM_ENTERSIZEMOVE
            case 0x005: // WM_SIZE
                if (Player.SnapWindow)
                    SnapManager.OnSizeAndEnterSizeMove(this);
                break;
            case 0x214: // WM_SIZING
                if (Player.KeepaspectWindow)
                {
                    RECT rc = Marshal.PtrToStructure<RECT>(m.LParam);
                    RECT r = rc;
                    SubtractWindowBorders(Handle, ref r, GetDpi(Handle), !Player.TitleBar);

                    int c_w = r.Right - r.Left, c_h = r.Bottom - r.Top;
                    Size videoSize = Player.VideoSize;

                    if (videoSize == Size.Empty)
                        videoSize = new Size(16, 9);

                    double aspect = videoSize.Width / (double)videoSize.Height;
                    int d_w = (int)Math.Ceiling(c_h * aspect - c_w);
                    int d_h = (int)Math.Floor(c_w / aspect - c_h);

                    int[] d_corners = { d_w, d_h, -d_w, -d_h };
                    int[] corners = { rc.Left, rc.Top, rc.Right, rc.Bottom };
                    int corner = GetResizeBorder(m.WParam.ToInt32());

                    if (corner >= 0)
                        corners[corner] -= d_corners[corner];

                    Marshal.StructureToPtr(new RECT(corners[0], corners[1], corners[2], corners[3]), m.LParam, false);
                    m.Result = new IntPtr(1);
                }
                return;
            case 0x84: // WM_NCHITTEST
                // resize borderless window
                if ((!Player.Border || !Player.TitleBar) && !Player.Fullscreen)
                {
                    const int HTCLIENT = 1;
                    const int HTLEFT = 10;
                    const int HTRIGHT = 11;
                    const int HTTOP = 12;
                    const int HTTOPLEFT = 13;
                    const int HTTOPRIGHT = 14;
                    const int HTBOTTOM = 15;
                    const int HTBOTTOMLEFT = 16;
                    const int HTBOTTOMRIGHT = 17;

                    int x = (short)(m.LParam.ToInt32() & 0xFFFF); // LoWord
                    int y = (short)(m.LParam.ToInt32() >> 16);    // HiWord

                    Point pt = PointToClient(new Point(x, y));
                    Size cs = ClientSize;
                    m.Result = new IntPtr(HTCLIENT);
                    int distance = FontHeight / 3;

                    if (pt.X >= cs.Width - distance && pt.Y >= cs.Height - distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTBOTTOMRIGHT);
                    else if (pt.X <= distance && pt.Y >= cs.Height - distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTBOTTOMLEFT);
                    else if (pt.X <= distance && pt.Y <= distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTTOPLEFT);
                    else if (pt.X >= cs.Width - distance && pt.Y <= distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTTOPRIGHT);
                    else if (pt.Y <= distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTTOP);
                    else if (pt.Y >= cs.Height - distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTBOTTOM);
                    else if (pt.X <= distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTLEFT);
                    else if (pt.X >= cs.Width - distance && cs.Height >= distance)
                        m.Result = new IntPtr(HTRIGHT);

                    return;
                }
                break;
            case 0x4A: // WM_COPYDATA
                {
                    var copyData = (CopyDataStruct)m.GetLParam(typeof(CopyDataStruct))!;
                    if (!MediaIpcMessage.TryParse(copyData.lpData, out string mode, out string[] args))
                        return;

                    switch (mode)
                    {
                        case "single":
                            Player.LoadFiles(args, true, false);
                            break;
                        case "queue":
                            Player.LoadFiles(args, true, true);
                            break;
                        case "command":
                            if (args.Length > 0)
                                Player.Command(args[0]);
                            break;
                    }

                    Activate();
                }
                return;
            case 0x216: // WM_MOVING
                if (Player.SnapWindow)
                    SnapManager.OnMoving(ref m);
                break;
        }

        if (m.Msg == _taskbarButtonCreatedMessage && Player.TaskbarProgress)
        {
            _taskbar = new Taskbar(Handle);
            ProgressTimer.Start();
        }

        // beep sound when closed using taskbar due to exception
        if (!IsDisposed)
            base.WndProc(ref m);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        if (_maxSizeSet)
        {
            BackgroundTaskRunner.Run(() => {
                Thread.Sleep(200);
                BeginInvoke(() => {
                    if (!IsDisposed && !Disposing)
                    {
                        MaximumSize = new Size(int.MaxValue, int.MaxValue);
                        _maxSizeSet = false;
                    }
                });
            });
        }
    }

    void CursorTimer_Tick(object sender, EventArgs e)
    {
        if (IsCursorPosDifferent(_lastCursorPosition))
        {
            _lastCursorPosition = MousePosition;
            _lastCursorChanged = Environment.TickCount;
        }
        else if ((Environment.TickCount - _lastCursorChanged > _cursorAutohide) &&
            ClientRectangle.Contains(PointToClient(MousePosition)) &&
            ActiveForm == this && !ContextMenu.IsVisible && !IsMouseInOsc())

            HideCursor();
    }

    void ProgressTimer_Tick(object sender, EventArgs e) => UpdateProgressBar();

    void UpdateProgressBar()
    {
        if (Player.TaskbarProgress && _taskbar != null)
            _taskbar.SetValue(Player.GetPropertyDouble("time-pos", false), Player.Duration.TotalSeconds);
    }

    void PropChangeOnTop(bool value) => BeginInvoke(() => TopMost = value);
    
    void PropChangeAid(string value)
    {
        Player.AID = value;
        Player.SchedulePlayerTask(_ => Player.UpdateTracks());
    }

    void PropChangeSid(string value) => Player.SID = value;

    void PropChangeVid(string value) => Player.VID = value;

    void PropChangeTitle(string value) { Title = value; SetTitle(); }
    
    void PropChangeEdition(int value) => Player.Edition = value;
    
    void PropChangeWindowMaximized()
    {
        if (!WasShown)
            return;

        BeginInvoke(() =>
        {
            Player.WindowMaximized = Player.GetPropertyBool("window-maximized");

            if (Player.WindowMaximized && WindowState != FormWindowState.Maximized)
                WindowState = FormWindowState.Maximized;
            else if (!Player.WindowMaximized && WindowState == FormWindowState.Maximized)
                WindowState = FormWindowState.Normal;
        });
    }

    void PropChangeWindowMinimized()
    {
        if (!WasShown)
            return;

        BeginInvoke(() =>
        {
            Player.WindowMinimized = Player.GetPropertyBool("window-minimized");

            if (Player.WindowMinimized && WindowState != FormWindowState.Minimized)
                WindowState = FormWindowState.Minimized;
            else if (!Player.WindowMinimized && WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
        });
    }

    void PropChangeCursorAutohide()
    {
        string strValue = Player.GetPropertyString("cursor-autohide");

        if (strValue == "no")
            _cursorAutohide = 0;
        else if (strValue == "always")
            _cursorAutohide = -1;
        else if (int.TryParse(strValue, out var intValue))
            _cursorAutohide = intValue;
    }

    void PropChangeBorder(bool enabled) {
        Player.Border = enabled;

        BeginInvoke(() => {
            if (!IsFullscreen)
            {
                if (Player.Border && FormBorderStyle == FormBorderStyle.None)
                    FormBorderStyle = FormBorderStyle.Sizable;

                if (!Player.Border && FormBorderStyle == FormBorderStyle.Sizable)
                    FormBorderStyle = FormBorderStyle.None;
            }
        });
    }

    void PropChangeTitleBar(bool enabled)
    {
        if (enabled == Player.TitleBar)
            return;

        Player.TitleBar = enabled;

        BeginInvoke(() => {
            SetSize(ClientSize.Width, ClientSize.Height, Screen.FromControl(this), false);
            Height += 1;
            Height -= 1;
        });
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _lastCycleFullscreen = Environment.TickCount;
        SetFormPosAndSize(false, true, true);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        ShowCursor();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (WindowState == FormWindowState.Maximized)
            Player.SetPropertyBool("window-maximized", true);

        WpfApplication.Init();
        Theme.UpdateWpfColors();
        MessageBoxEx.MessageForeground = Theme.Current?.GetBrush("heading");
        MessageBoxEx.MessageBackground = Theme.Current?.GetBrush("background");
        MessageBoxEx.ButtonBackground  = Theme.Current?.GetBrush("highlight");
        InitAndBuildContextMenu();
        Cursor.Position = new Point(Cursor.Position.X + 1, Cursor.Position.Y);
        GlobalHotkey.RegisterGlobalHotkeys(Handle);
        StrongReferenceMessenger.Default.Send(new MainWindowIsLoadedMessage());
        WasShown = true;
    }

    void ContextMenu_Closed(object sender, System.Windows.RoutedEventArgs e) => MenuAutoResetEvent.Set();

    void DisposeManagedResources()
    {
        if (_managedResourcesDisposed)
            return;

        _managedResourcesDisposed = true;

        Player.FileLoaded -= Player_FileLoaded;
        Player.Pause -= Player_Pause;
        Player.PlaylistPosChanged -= Player_PlaylistPosChanged;
        Player.Seek -= UpdateProgressBar;
        Player.Shutdown -= Player_Shutdown;
        Player.VideoSizeChanged -= Player_VideoSizeChanged;
        Player.ClientMessage -= Player_ClientMessage;

        GuiCommand.Current.ScaleWindow -= GuiCommand_ScaleWindow;
        GuiCommand.Current.MoveWindow -= GuiCommand_MoveWindow;
        GuiCommand.Current.WindowScaleNet -= GuiCommand_WindowScaleNet;
        GuiCommand.Current.ShowMenu -= GuiCommand_ShowMenu;

        if (_contextMenuClosedHandlerAttached)
        {
            ContextMenu.Closed -= ContextMenu_Closed;
            _contextMenuClosedHandlerAttached = false;
        }

        ContextMenu.Items.Clear();
        GlobalHotkey.UnregisterGlobalHotkeys();
        _taskbar?.Dispose();
        _taskbar = null;
        MenuAutoResetEvent.Set();
        MenuAutoResetEvent.Dispose();

        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SaveWindowProperties();
        
        if (FormBorderStyle != FormBorderStyle.None)
        {
            if (WindowState == FormWindowState.Maximized)
                _wasMaximized = true;
            else if (WindowState == FormWindowState.Normal)
                _wasMaximized = false;
        }

        if (WasShown)
        {
            if (WindowState == FormWindowState.Minimized)
                Player.SetPropertyBool("window-minimized", true);
            else if (WindowState == FormWindowState.Normal)
            {
                Player.SetPropertyBool("window-maximized", false);
                Player.SetPropertyBool("window-minimized", false);
            }
            else if (WindowState == FormWindowState.Maximized)
                Player.SetPropertyBool("window-maximized", true);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (Player.IsQuitNeeded)
            Player.CommandV("quit");

        if (!Player.ShutdownAutoResetEvent.WaitOne(10000))
            Msg.ShowError(_("Shutdown thread failed to complete within 10 seconds."));

        Player.Destroy();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _mouseDownLocation = PointToScreen(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsCursorPosDifferent(_mouseDownLocation) &&
            WindowState == FormWindowState.Normal &&
            e.Button == MouseButtons.Left && !IsMouseInOsc() &&
            Player.GetPropertyBool("window-dragging"))
        {
            var HTCAPTION = new IntPtr(2);
            var WM_NCLBUTTONDOWN = 0xA1;
            ReleaseCapture();
            PostMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        SaveWindowProperties();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // prevent annoying beep using alt key
        if (ModifierKeys == Keys.Alt)
            e.SuppressKeyPress = true;

        base.OnKeyDown(e);
    }

    [DllImport("DwmApi")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, int[] attrValue, int attrSize);
}
