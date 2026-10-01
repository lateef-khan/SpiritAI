using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SpiritAI.CallLog;

/// <summary>
/// The calls waiting to be copied.
/// </summary>
public sealed class CallLogQueue
{
    private readonly Channel<string> live = Channel.CreateUnbounded<string>();

    private readonly Channel<string> backlog = Channel.CreateUnbounded<string>();

    /// <summary>How many calls wait, live and backlog together.</summary>
    public int Waiting => live.Reader.Count + backlog.Reader.Count;

    /// <summary>Puts one call on the backlog.</summary>
    /// <param name="callId">The call's <c>conversationSpaceId</c>.</param>
    public void Add(string callId) => backlog.Writer.TryWrite(callId);

    /// <summary>Puts one call that just ended ahead of the backlog.</summary>
    /// <param name="callId">The call's <c>conversationSpaceId</c>.</param>
    public void AddLive(string callId) => live.Writer.TryWrite(callId);

    /// <summary>Gives each call as it arrives, every waiting live call before any backlog call.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The calls, live first, each kind oldest first.</returns>
    public async IAsyncEnumerable<string> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            if (live.Reader.TryRead(out var callId) || backlog.Reader.TryRead(out callId))
            {
                yield return callId;
                continue;
            }

            await WhenEitherHasACallAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WhenEitherHasACallAsync(CancellationToken cancellationToken)
    {
        using var lose = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var first = await Task.WhenAny(
                live.Reader.WaitToReadAsync(lose.Token).AsTask(),
                backlog.Reader.WaitToReadAsync(lose.Token).AsTask())
            .ConfigureAwait(false);

        // Ends the other wait, so no waiter is left behind on its channel.
        await lose.CancelAsync().ConfigureAwait(false);
        await first.ConfigureAwait(false);
    }
}
