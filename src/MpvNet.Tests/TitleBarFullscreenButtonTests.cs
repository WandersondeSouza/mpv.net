using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using MpvNet.Windows.Native;
using MpvNet.Windows.WinForms;
using Xunit;

public sealed class TitleBarFullscreenButtonTests
{
    [Fact]
    public void CaptionPreservesNativeFrameTitleClientAndHitTargets() => OnWindowThread(owner =>
    {
        Size clientSize = owner.ClientSize;
        string title = owner.Text;
        nint handle = owner.Handle;
        WinApi.DwmGetWindowAttribute(handle, 5, out WinApi.RECT before, 16);
        using var caption = new TitleBarFullscreenButton(owner, () => { });

        foreach (FormWindowState state in new[] { FormWindowState.Normal, FormWindowState.Maximized })
        {
            owner.WindowState = state;
            Application.DoEvents();
            Size originalClient = owner.ClientSize;
            WinApi.DwmGetWindowAttribute(handle, 5, out WinApi.RECT buttons, 16);
            WinApi.GetWindowRect(handle, out WinApi.RECT window);
            Rectangle native = buttons.ToRectangle();
            native.Offset(window.Left, window.Top);
            Point maximize = new(native.Left + native.Width / 2, native.Top + native.Height / 2);
            int originalHit = HitTest(handle, maximize);
            caption.UpdateCaption(true, false, "Tela cheia");
            Application.DoEvents();
            Assert.True(caption.Visible);
            Assert.Same(owner, caption.Owner);
            Assert.False(caption.ShowInTaskbar);
            Assert.Equal(handle, owner.Handle);
            Assert.Equal(title, owner.Text);
            Assert.Equal(originalClient, owner.ClientSize);

            Assert.True(caption.Right <= native.Left);
            Assert.True(caption.Bottom <= owner.PointToScreen(Point.Empty).Y);
            // The title passes through to the unmodified native caption (drag,
            // double click, system menu). The native hit test remains unchanged.
            Point titlePoint = caption.PointToScreen(new Point(20, caption.Height / 2));
            Assert.Equal(-1, HitTest(caption.Handle, titlePoint));
            Assert.Equal(2, HitTest(handle, titlePoint));
            Assert.Equal(originalHit, HitTest(handle, maximize));
        }

        owner.WindowState = FormWindowState.Normal;
        Application.DoEvents();
        Assert.Equal(clientSize, owner.ClientSize);
        WinApi.DwmGetWindowAttribute(handle, 5, out WinApi.RECT after, 16);
        Assert.Equal(before.ToRectangle(), after.ToRectangle());
    });

    [Theory]
    [InlineData(FormWindowState.Normal)]
    [InlineData(FormWindowState.Maximized)]
    public void LocalizedAccessibleButtonInvokesOnceAndCanBeHiddenAndRecreated(FormWindowState state) => OnWindowThread(owner =>
    {
        owner.WindowState = state;
        Application.DoEvents();
        int commands = 0;
        using var caption = new TitleBarFullscreenButton(owner, () => commands++);
        caption.UpdateCaption(true, true, "Tela cheia");
        var button = Assert.IsAssignableFrom<Button>(Assert.Single(caption.Controls));
        Assert.Equal("Tela cheia", button.AccessibilityObject.Name);
        Assert.Equal(AccessibleRole.PushButton, button.AccessibilityObject.Role);
        button.AccessibilityObject.DoDefaultAction();
        Assert.Equal(1, commands);
        Assert.Equal(state, owner.WindowState);

        caption.UpdateCaption(true, false, "Fullscreen");
        Assert.Equal("Fullscreen", button.AccessibilityObject.Name);
        caption.UpdateCaption(false, false, "Fullscreen");
        Assert.False(caption.Visible);
        owner.WindowState = FormWindowState.Minimized;
        caption.UpdateCaption(true, false, "Fullscreen");
        Assert.False(caption.Visible);
        owner.WindowState = FormWindowState.Normal;
        owner.RecreateWindow();
        Assert.False(caption.Visible);
        caption.UpdateCaption(true, true, "Tela cheia");
        Assert.True(caption.Visible);
        Assert.Same(owner, caption.Owner);
        button.AccessibilityObject.DoDefaultAction();
        Assert.Equal(2, commands);
        caption.Dispose();
        caption.Dispose();
        Assert.True(button.IsDisposed);
    });

    static int HitTest(nint window, Point point) => WinApi.SendMessage(window, 0x0084, 0,
        new nint(unchecked((int)((uint)(ushort)point.X | ((uint)(ushort)point.Y << 16))))).ToInt32();

    static void OnWindowThread(Action<TestWindow> test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            nint previousDpi = SetThreadDpiAwarenessContext(new nint(-4)); // Match the player's PerMonitorV2 manifest.
            try
            {
                using var owner = new TestWindow { Text = "MPV.NET caption test", Size = new Size(640, 360) };
                owner.Show();
                Application.DoEvents();
                test(owner);
            }
            catch (Exception ex) { failure = ex; }
            finally { SetThreadDpiAwarenessContext(previousDpi); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Caption UI test timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    sealed class TestWindow : Form
    {
        public void RecreateWindow() => RecreateHandle();
    }

    [DllImport("user32.dll")]
    static extern nint SetThreadDpiAwarenessContext(nint context);
}
