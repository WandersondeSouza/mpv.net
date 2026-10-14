using System;
using MpvNet;

using Xunit;

namespace MpvNet.Tests;

public sealed class MpvClientReliabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HookIsContinuedExactlyOnceEvenWhenCallbackThrows(bool fail)
    {
        using RecordingHookClient client = new() { Fail = fail };
        MpvEventSnapshot snapshot = MpvEventSnapshotTests.CreateHookSnapshot(789, 1);
        if (fail)
            Assert.Throws<InvalidOperationException>(() => client.ProcessHook(snapshot));
        else
            client.ProcessHook(snapshot);
        Assert.Equal(1, client.Continued);
        Assert.Equal(789UL, client.LastId);
    }

    [Fact]
    public void HookNativeCallsAreRejectedAfterShutdown()
    {
        using MpvClient client = new();
        client.BeginShutdown();
        // A nonzero fake handle would crash if either native call escaped the gate.
        client.Handle = (nint)1;
        try
        {
            Assert.False(client.RegisterHook("on_load", 1, 50));
            client.ContinueHook(1);
        }
        finally
        {
            client.Handle = 0;
        }
    }

    [Fact]
    public void TryGetPropertyStringReturnsUnavailableWithoutAPlayerHandle()
    {
        using MpvClient client = new();

        Assert.False(client.TryGetPropertyString("user-data/mpv/ytdl/path", out string value));
        Assert.Equal("", value);
    }

    sealed class RecordingHookClient : MpvClient
    {
        public bool Fail;
        public int Continued;
        public ulong LastId;
        internal override void OnHook(MpvEventSnapshot snapshot)
        {
            if (Fail)
                throw new InvalidOperationException("Hook test failure");
        }
        internal override void ContinueHook(ulong id)
        {
            Continued++;
            LastId = id;
        }
    }

    [Fact]
    public void QueueOverflowIsCountedAndNotified()
    {
        using MpvClient client = new();
        int notificationCount = 0;
        client.EventQueueOverflow += () => notificationCount++;

        client.OnQueueOverflow();
        client.OnQueueOverflow();

        Assert.Equal(2, client.EventQueueOverflowCount);
        Assert.Equal(2, notificationCount);
    }
}
