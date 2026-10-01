using SpiritAI.CallLog;

using Xunit;

namespace SpiritAI.Tests.CallLog;

public sealed class CallLogQueueTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALiveCallIsReadBeforeTheBacklogQueuedAheadOfIt()
    {
        var queue = new CallLogQueue();

        queue.Add("b1");
        queue.Add("b2");
        queue.AddLive("l1");

        Assert.Equal(["l1", "b1", "b2"], await queue.ReadAllAsync(Cancel).Take(3).ToListAsync(Cancel));
    }

    [Fact]
    public async Task ALiveCallAddedWhileTheReaderWaitsIsRead()
    {
        var queue = new CallLogQueue();
        await using var calls = queue.ReadAllAsync(Cancel).GetAsyncEnumerator(Cancel);

        var next = calls.MoveNextAsync();
        queue.AddLive("l1");

        Assert.True(await next.AsTask().WaitAsync(TimeSpan.FromSeconds(5), Cancel));
        Assert.Equal("l1", calls.Current);
    }
}
