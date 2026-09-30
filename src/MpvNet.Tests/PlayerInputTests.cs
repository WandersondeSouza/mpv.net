using System;
using System.Linq;

using MpvNet.Windows.UI;
using Xunit;

namespace MpvNet.Tests;

public sealed class PlayerInputTests
{
    const string PlayPause = "script-message-to mpvnet play-pause";
    const string VideoClick = "expand-properties script-message-to mpvnet video-click ${user-data/mpvnet-click}";

    [Theory]
    [InlineData("MBTN_Left", VideoClick)]
    [InlineData("MBTN_Left_DBL", "cycle fullscreen")]
    [InlineData("Space", PlayPause)]
    [InlineData("p", PlayPause)]
    [InlineData("P", PlayPause)]
    [InlineData("MBTN_Mid", PlayPause)]
    [InlineData("Wheel_Up", "add volume 2")]
    [InlineData("Wheel_Down", "add volume -2")]
    [InlineData("MBTN_Right", "script-message-to mpvnet show-menu")]
    [InlineData("MBTN_Back", "playlist-prev")]
    [InlineData("MBTN_Forward", "playlist-next")]
    [InlineData("Enter", "cycle fullscreen")]
    [InlineData("f", "cycle fullscreen")]
    [InlineData("Esc", "quit")]
    public void DefaultsSurviveSerialization(string key, string command)
    {
        var bindings = InputHelp.Parse(InputHelp.ConvertToString(InputHelp.GetDefaults()));
        Assert.Equal(command, Assert.Single(bindings, b => b.Input == key).Command);
    }

    [Theory]
    [InlineData("MBTN_Left")]
    [InlineData("MBTN_Left_DBL")]
    [InlineData("p")]
    [InlineData("P")]
    [InlineData("Space")]
    public void ExplicitOverrideWinsWithoutRewritingFile(string key)
    {
        var conf = new InputConf("") { Content = key + " show-progress" };
        var bindings = InputHelp.GetActiveBindings(InputHelp.Parse(conf.GetContent()));
        Assert.Equal("show-progress", bindings[key].Command);
        Assert.Equal(key + " show-progress", conf.Content);
    }

    [Theory]
    [InlineData("MBTN_Left")]
    [InlineData("MBTN_Left_DBL")]
    [InlineData("p")]
    [InlineData("P")]
    [InlineData("Space")]
    public void ExplicitIgnoreRemainsLastBinding(string key)
    {
        var conf = new InputConf("") { Content = key + " ignore" };
        var bindings = InputHelp.Parse(conf.GetContent());
        Assert.Empty(bindings.Last(b => b.Input == key).Command);
    }

    [Fact]
    public void MenuInputConfKeepsUserBindingsAndDoesNotInjectDefaults()
    {
        const string content = "p show-progress #menu: View > Progress\nMBTN_Left ignore";
        var conf = new InputConf("") { Content = content };
        // Avoid the unrelated legacy menu migration: inspect the menu/editor path.
        var bindings = conf.GetBindings().confBindings!;
        Assert.Equal("show-progress", bindings.Single(b => b.Input == "p").Command);
        Assert.Empty(bindings.Single(b => b.Input == "MBTN_Left").Command);
        Assert.DoesNotContain(bindings, b => b.Input == "P");
        Assert.Equal(content, conf.Content);
    }

    [Fact]
    public void NamedPauseAliasDoesNotCollapseOtherAliasesInEditorOrRuntime()
    {
        const string content = "p " + PlayPause + " # Play/Pause";
        var conf = new InputConf("") { Content = content };
        foreach (var bindings in new[] {
            InputHelp.Parse(conf.GetContent()), InputHelp.GetEditorBindings(content), conf.GetBindings().menuBindings })
        {
            foreach (string key in new[] { "p", "P", "Space", "MBTN_Mid" })
                Assert.Equal(PlayPause, Assert.Single(bindings, b => b.Input == key).Command);
        }
    }

    [Fact]
    public void ProgressRemainsAvailableInMenuWithoutCompetingWithP()
    {
        var progress = Assert.Single(InputHelp.GetDefaults(), b => b.Command == "show-progress");
        Assert.True(progress.IsMenu);
        Assert.Empty(progress.Input);
        var bindings = InputHelp.Parse("p show-progress\nP " + PlayPause);
        Assert.Equal(new[] { "p", "P" }, bindings.Select(b => b.Input));
    }

    [Fact]
    public void SingleClickWaitsOnlyRemainingWindowsIntervalAndRunsOnce()
    {
        var gesture = new VideoClickArbiter(500);
        long id = gesture.Begin(true, 1000);
        Assert.True(gesture.TrySchedule(id, 1120, out int delay));
        Assert.Equal(380, delay);
        Assert.False(gesture.TrySchedule(id, 1150, out _));
        Assert.True(gesture.TryComplete(id));
        Assert.False(gesture.TryComplete(id));
    }

    [Fact]
    public void LongPressDoesNotWaitAnotherDoubleClickInterval()
    {
        var gesture = new VideoClickArbiter(500);
        long id = gesture.Begin(true, 1000);
        Assert.True(gesture.TrySchedule(id, 1700, out int delay));
        Assert.Equal(1, delay);
    }

    [Fact]
    public void DoubleClickDragMediaChangeAndShutdownInvalidateQueuedAndScheduledClicks()
    {
        var gesture = new VideoClickArbiter(500);
        long first = gesture.Begin(true, 1000);
        Assert.True(gesture.TrySchedule(first, 1050, out _));
        long cancelled = gesture.Invalidate();
        Assert.False(gesture.TryComplete(first));
        Assert.False(gesture.TrySchedule(first, 1100, out _));
        // The trailing mouse-up after a double click must not become a single click.
        Assert.False(gesture.TrySchedule(cancelled, 1100, out _));
        long second = gesture.Begin(true, 2000);
        Assert.False(gesture.TrySchedule(first, 2050, out _));
        Assert.True(gesture.TrySchedule(second, 2050, out _));
    }

    [Fact]
    public void OscMenusAndEmptyPlayerAreIneligible()
    {
        var gesture = new VideoClickArbiter(500);
        long id = gesture.Begin(false, 1000);
        Assert.False(gesture.TrySchedule(id, 1100, out _));
        Assert.False(gesture.TryComplete(id));
    }
}
