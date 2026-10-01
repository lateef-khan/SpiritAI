using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SpiritAI.PublicChat;

using Xunit;

namespace SpiritAI.Tests.PublicChat;

/// <summary>
/// A sweep that times out is a failed sweep, not a stopped host: <see cref="TaskCanceledException"/>
/// is what an <see cref="HttpClient"/> timeout throws, and it must not end the background service.
/// </summary>
public sealed class WidgetTranscriptSweeperTimeoutTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ATimedOutSweepIsLoggedAndTheSweeperKeepsRunning()
    {
        var scopes = new TimingOutScopes(timeouts: 1);
        var logger = new CapturingLogger<WidgetTranscriptSweeper>();
        using var sweeper = new WidgetTranscriptSweeper(
            scopes,
            Options.Create(new PublicChatOptions()),
            new NoWaitClock(),
            logger);

        await sweeper.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            // The first tick fires at once, so a sweeper that survived the timeout sweeps again.
            await Task.WhenAny(scopes.SecondSweep, sweeper.ExecuteTask!).WaitAsync(Bound, TestContext.Current.CancellationToken);

            Assert.True(scopes.SecondSweep.IsCompleted, "The sweeper stopped after the timeout.");
            Assert.False(sweeper.ExecuteTask!.IsFaulted);
        }
        finally
        {
            await sweeper.StopAsync(CancellationToken.None);
        }

        Assert.True(sweeper.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Exception is TaskCanceledException);
    }

    /// <summary>Throws a timeout the first <c>timeouts</c> times a scope is asked for, and signals the next.</summary>
    private sealed class TimingOutScopes(int timeouts) : IServiceScopeFactory
    {
        private readonly TaskCompletionSource secondSweep = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int calls;

        public Task SecondSweep => secondSweep.Task;

        public IServiceScope CreateScope()
        {
            if (Interlocked.Increment(ref calls) <= timeouts)
            {
                throw new TaskCanceledException("The HTTP request timed out.");
            }

            secondSweep.TrySetResult();
            throw new InvalidOperationException("The test needs no further sweep.");
        }
    }
}
