using System;
using System.IO;

using MpvNet;

using Xunit;

namespace MpvNet.Tests;

public sealed class FileLoggingTests
{
    [Fact]
    public void DebugAndBothErrorOverloadsFollowTheBuildSwitch()
    {
#if ENABLE_FILE_LOGGING
        bool expected = true;
#else
        bool expected = false;
#endif
        Assert.Equal(expected, Log.IsEnabled);
        Assert.Equal(expected, Log.DiagnosticFileLoggingEnabled);
        string marker = "file-logging-" + Guid.NewGuid().ToString("N");
        string path = Path.Combine(Log.LogFolder, $"mpvnet-{DateTime.Now:yyyy-MM-dd}.log");
        string before = File.Exists(path) ? File.ReadAllText(path) : "";

        Log.Debug(marker + " debug");
        Log.Error(marker + " error");
        Log.Error(new InvalidOperationException(marker + " exception"), marker + " context");

        string after = File.Exists(path) ? File.ReadAllText(path) : "";
        if (expected)
        {
            Assert.Contains("[DEBUG] " + marker + " debug", after);
            Assert.Contains("[ERROR] " + marker + " error", after);
            Assert.Contains("[ERROR] " + marker + " context", after);
            Assert.Contains("InvalidOperationException: " + marker + " exception", after);
        }
        else
        {
            Assert.Equal(before, after);
            Assert.DoesNotContain(marker, after);
        }
    }
}
