using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using MpvNet.Native;
using Xunit;

namespace MpvNet.Tests;

public sealed class RuntimeInputBindingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StrongIgnoreOverridesWeakScriptBindingRegardlessOfOrder(bool reverse)
    {
        var bindings = new[] {
            new { key = "p", cmd = "script-binding example/play", priority = 100, is_weak = true },
            new { key = "p", cmd = "ignore", priority = 0, is_weak = false }
        };
        string json = JsonSerializer.Serialize(reverse ? bindings.Reverse() : bindings);
        Binding active = Assert.Single(RuntimeInputBindings.Parse(json));
        Assert.Equal("ignore", active.Command);
        Assert.Contains("ignore", InputHelp.ConvertToString([active]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HighestPriorityWinsWithinTheSameStrengthAndInactiveSectionsAreExcluded(bool weak)
    {
        string json = JsonSerializer.Serialize(new[] {
            new { key = "Space", cmd = "cycle pause", priority = 0, is_weak = weak },
            new { key = "Space", cmd = "show-progress", priority = 5, is_weak = weak },
            new { key = "Space", cmd = "quit", priority = 1, is_weak = weak },
            new { key = "Esc", cmd = "quit", priority = -1, is_weak = weak }
        });
        Binding active = Assert.Single(RuntimeInputBindings.Parse(json));
        Assert.Equal("Space", active.Input);
        Assert.Equal("show-progress", active.Command);
    }

    [Fact]
    public void ScriptBindingsCaseUnicodeAndCommentsSurviveExportWithoutParsingTheirCommands()
    {
        const string json = """
            [
              {"key":"p","cmd":"show-text \"áudio #1\"; cycle pause","priority":0,"is_weak":false,"comment":"Meu atalho"},
              {"key":"P","cmd":"script-binding example/play","priority":0,"is_weak":true,"owner":"example"},
              {"key":"ç","cmd":"show-progress","priority":0,"is_weak":false}
            ]
            """;
        var active = RuntimeInputBindings.Parse(json);
        Assert.Equal(new[] { "P", "p", "ç" }, active.Select(binding => binding.Input));
        Assert.Equal("script-binding example/play", active[0].Command);
        Assert.Equal("show-text \"áudio #1\"; cycle pause", active[1].Command);
        Assert.Equal("Meu atalho", active[1].Comment);
        Assert.Contains("áudio #1", InputHelp.ConvertToString(active));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("[]")]
    public void UnavailableOrEmptySnapshotDoesNotReportStartupBindingsAsActive(string json) =>
        Assert.Empty(RuntimeInputBindings.Parse(json));

    [Fact]
    public void NativeSnapshotUsesExplicitInputFileAndReflectsSectionChanges()
    {
        using TestDirectory directory = new();
        string input = Path.Combine(directory.Path, "explicit-input.conf");
        const string content = "p ignore\np show-text duplicate-wins\nP ignore\n";
        File.WriteAllText(input, content);
        using MainPlayer player = new();
        player.MainHandle = LibMpv.mpv_create();
        Assert.NotEqual(IntPtr.Zero, player.MainHandle);
        foreach (var (key, value) in new[] { ("config", "no"), ("vo", "null"), ("ao", "null"),
            ("idle", "yes"), ("load-scripts", "no"), ("input-builtin-bindings", "no"), ("input-conf", input) })
            Assert.True(LibMpv.mpv_set_option_string(player.MainHandle,
                Encoding.UTF8.GetBytes(key + "\0"), Encoding.UTF8.GetBytes(value + "\0")) >= 0, key);
        Assert.Equal(0, (int)LibMpv.mpv_initialize(player.MainHandle));
        player.SetMpvInitialized();
        player.Handle = LibMpv.mpv_create_client(player.MainHandle, "input-tests");
        Assert.NotEqual(IntPtr.Zero, player.Handle);

        var bindings = RuntimeInputBindings.Parse(player.GetPropertyString("input-bindings"));
        Assert.Equal("show-text duplicate-wins", Assert.Single(bindings, b => b.Input == "p").Command);
        Assert.Equal("ignore", Assert.Single(bindings, b => b.Input == "P").Command);
        Assert.DoesNotContain(bindings, b => b.Input == "Space");
        player.CommandV("define-section", "input-test", "p show-text section-wins", "force");
        player.CommandV("enable-section", "input-test");
        bindings = RuntimeInputBindings.Parse(player.GetPropertyString("input-bindings"));
        Assert.Equal("show-text section-wins", Assert.Single(bindings, b => b.Input == "p").Command);
        player.CommandV("disable-section", "input-test");
        bindings = RuntimeInputBindings.Parse(player.GetPropertyString("input-bindings"));
        Assert.Equal("show-text duplicate-wins", Assert.Single(bindings, b => b.Input == "p").Command);
        Assert.Equal(content, File.ReadAllText(input));
    }
}
