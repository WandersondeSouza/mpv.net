using System;
using System.IO;
using System.Runtime.CompilerServices;

using MpvNet;

using Xunit;

namespace MpvNet.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Set the override before AppPaths or the static file logger is initialized.
        // Keep an explicit override supplied by CI or a developer.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MPVNET_LOCAL_APPDATA")))
            Environment.SetEnvironmentVariable(
                "MPVNET_LOCAL_APPDATA", Path.Combine(AppContext.BaseDirectory, "test-runtime"));
    }
}

public sealed class TestEnvironmentTests
{
    [Fact]
    public void ApplicationDataAndLogsUseTheTestOverride()
    {
        string? configuredPath = Environment.GetEnvironmentVariable("MPVNET_LOCAL_APPDATA");

        Assert.False(string.IsNullOrEmpty(configuredPath));
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPath!)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppPaths.LocalAppData)));
        Assert.Equal(Path.Combine(AppPaths.LocalAppData, "mpv.net", "Logs"), Log.LogFolder);
    }
}
