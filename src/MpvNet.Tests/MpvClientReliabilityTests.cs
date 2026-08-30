using MpvNet;

using Xunit;

namespace MpvNet.Tests;

public sealed class MpvClientReliabilityTests
{
    [Fact]
    public void QueueOverflowIsCountedAndNotified()
    {
        MpvClient client = new();
        int notificationCount = 0;
        client.EventQueueOverflow += () => notificationCount++;

        client.OnQueueOverflow();
        client.OnQueueOverflow();

        Assert.Equal(2, client.EventQueueOverflowCount);
        Assert.Equal(2, notificationCount);
    }
}
