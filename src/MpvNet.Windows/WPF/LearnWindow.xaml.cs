
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Forms;

using MpvNet.Help;
using MpvNet.Windows.UI;

namespace MpvNet.Windows;

public partial class LearnWindow : Window
{
    public Binding? InputItem { get; set; }
    string _newKey = "";

    const uint MapVirtualKeyToScanCode = 0;

    const int VirtualKeyMenu = 0x12;
    const int VirtualKeyLeftMenu = 0xA4;
    const int VirtualKeyRightMenu = 0xA5;

    const int VirtualKeyControl = 0x11;
    const int VirtualKeyLeftControl = 0xA2;
    const int VirtualKeyRightControl = 0xA3;

    bool _blockLeftMouseButton;
    bool _blockRightMouseButton;

    public LearnWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public Theme? Theme => Theme.Current;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern short GetKeyState(int keyCode);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        StringBuilder pwszBuff, int cchBuff, uint wFlags);

    [DllImport("user32.dll")]
    static extern bool GetKeyboardState(byte[] lpKeyState);

    string ToUnicode(uint vk, ref bool firstEmpty)
    {
        byte[] keys = new byte[256];

        if (!GetKeyboardState(keys))
            return "";

        if ((keys[VirtualKeyControl] & 0x80) != 0 && (keys[VirtualKeyMenu] & 0x80) == 0)
            keys[VirtualKeyLeftControl] = keys[VirtualKeyRightControl] = keys[VirtualKeyControl] = 0;

        uint scanCode = MapVirtualKey(vk, MapVirtualKeyToScanCode);

        string ret = ToUnicode(vk, scanCode, keys);

        firstEmpty = ret == "";

        if (firstEmpty)
        {
            keys[VirtualKeyLeftControl] = keys[VirtualKeyRightControl] = keys[VirtualKeyControl] = 0;
            keys[VirtualKeyLeftMenu] = keys[VirtualKeyRightMenu] = keys[VirtualKeyMenu] = 0;
            ret = ToUnicode(vk, scanCode, keys);
        }

        if (ret.Length == 1 && ret[0] < 32)
            return "";

        return ret;
    }

    public string ToUnicode(uint vk, uint scanCode, byte[] keys)
    {
        StringBuilder sb = new StringBuilder(10);
        ToUnicode(vk, scanCode, keys, sb, sb.Capacity, 0);
        return sb.ToString();
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        Message m = new Message();
        m.HWnd = hwnd;
        m.Msg = msg;
        m.WParam = wParam;
        m.LParam = lParam;
        ProcessKeyEventArgs(ref m);
        return m.Result;
    }

    void OnKeyDown(uint vk)
    {
        bool firstEmpty = false;
        Keys key = (Keys)vk;

        if (key == Keys.ControlKey ||
            key == Keys.ShiftKey ||
            key == Keys.Menu ||
            key == Keys.None ||
            key == Keys.Tab)

            return;

        string text = ToUnicode(vk, ref firstEmpty);

        if ((int)key > 111 && (int)key < 136)
            text = "F" + ((int)key - 111);

        if ((int)key > 95 && (int)key < 106)
            text = "KP" + ((int)key - 96);

        switch (text)
        {
            case "#":  text = "Sharp"; break;
            case "´´": text = "´"; break;
            case "``": text = "`"; break;
            case "^^": text = "^"; break;
        }

        switch (key)
        {
            case Keys.Left:               text = "Left"; break;
            case Keys.Up:                 text = "Up"; break;
            case Keys.Right:              text = "Right"; break;
            case Keys.Down:               text = "Down"; break;
            case Keys.Space:              text = "Space"; break;
            case Keys.Enter:              text = "Enter"; break;
            case Keys.Tab:                text = "Tab"; break;
            case Keys.Back:               text = "BS"; break;
            case Keys.Delete:             text = "Del"; break;
            case Keys.Insert:             text = "Ins"; break;
            case Keys.Home:               text = "Home"; break;
            case Keys.End:                text = "End"; break;
            case Keys.PageUp:             text = "PGUP"; break;
            case Keys.PageDown:           text = "PGDWN"; break;
            case Keys.Escape:             text = "Esc"; break;
            case Keys.Sleep:              text = "Sleep"; break;
            case Keys.Cancel:             text = "Cancel"; break;
            case Keys.PrintScreen:        text = "Print"; break;
            case Keys.BrowserFavorites:   text = "Favorites"; break;
            case Keys.BrowserSearch:      text = "Search"; break;
            case Keys.BrowserHome:        text = "Homepage"; break;
            case Keys.LaunchMail:         text = "Mail"; break;
            case Keys.Play:               text = "Play"; break;
            case Keys.Pause:              text = "Pause"; break;
            case Keys.MediaPlayPause:     text = "PlayPause"; break;
            case Keys.MediaStop:          text = "Stop"; break;
            case Keys.MediaNextTrack:     text = "Next"; break;
            case Keys.MediaPreviousTrack: text = "Prev"; break;

            case Keys.VolumeUp:
            case Keys.VolumeDown:
            case Keys.VolumeMute:
                text = ""; break;
        }

        bool isAlt   = GetKeyState(18) < 0;
        bool isShift = GetKeyState(16) < 0;
        bool isCtrl  = GetKeyState(17) < 0;

        bool isLetter = (int)key > 64 && (int)key < 91;

        if (isLetter && isShift)
            text = text.ToUpper();

        string keyString = ToUnicode(vk, ref firstEmpty);

        if (isAlt && !isCtrl)
            text = "Alt+" + text;

        if (isShift && (keyString == "" || keyString == " "))
            text = "Shift+" + text;

        if (isCtrl && isAlt && firstEmpty)
            text = "Ctrl+Alt+" + text;
        else if (isCtrl && !(keyString != "" && isCtrl && isAlt))
            text = "Ctrl+" + text;

        if (!string.IsNullOrEmpty(text))
            SetKey(text);
    }

    void SetKey(string? key)
    {
        _newKey = key!;
        KeyTextBlock.Text = key;
    }

    void ProcessKeyEventArgs(ref Message m)
    {
        const int KeyDownMessage = 0x100;
        const int SystemKeyDownMessage = 0x104;
        const int AppCommandMessage = 0x319;

        if (m.Msg == KeyDownMessage || m.Msg == SystemKeyDownMessage)
            OnKeyDown((uint)m.WParam.ToInt64());
        else if (m.Msg == AppCommandMessage)
        {
            string? value = MpvHelp.WM_APPCOMMAND_to_mpv_key((int)(m.LParam.ToInt64() >> 16 & ~0xf000));

            if (value != null)
                SetKey(value);
        }
    }

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(new HwndSourceHook(WndProc));
        SetKey(InputItem?.Input);
    }

    void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        InputItem!.Input = _newKey;
        Close();
    }

    void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        InputItem!.Input = "";
        Close();
    }

    void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
            SetKey(GetModifierText() + "WHEEL_UP");
        else
            SetKey(GetModifierText() + "WHEEL_DOWN");
    }

    void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        switch (e.ChangedButton)
        {
            case MouseButton.Left:
                if (_blockLeftMouseButton)
                    _blockLeftMouseButton = false;
                else
                    SetKey(GetModifierText() + "MBTN_LEFT");
                break;
            case MouseButton.Right:
                if (_blockRightMouseButton)
                    _blockRightMouseButton = false;
                else
                    SetKey(GetModifierText() + "MBTN_RIGHT");
                break;
            case MouseButton.Middle:
                SetKey(GetModifierText() + "MBTN_MID");
                break;
            case MouseButton.XButton1:
                SetKey(GetModifierText() + "MBTN_BACK");
                break;
            case MouseButton.XButton2:
                SetKey(GetModifierText() + "MBTN_FORWARD");
                break;
        }
    }

    void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            SetKey(GetModifierText() + "MBTN_LEFT_DBL");
            _blockLeftMouseButton = true;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            SetKey(GetModifierText() + "MBTN_RIGHT_DBL");
            _blockRightMouseButton = true;
        }
    }

    string GetModifierText()
    {
        string ret = "";

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            ret = "Alt+" + ret;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            ret = "Ctrl+" + ret;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            ret = "Shift+" + ret;

        return ret;
    }
}
