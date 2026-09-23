using System.Threading.Channels;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The line between the webhook route and <see cref="ChatwootEventWorker"/>. Chatwoot waits five
/// seconds for an answer, so the route only puts the event here and answers at once.
/// </summary>
public sealed class ChatwootEventQueue
{
    /// <summary>How many events may wait. Past it the oldest waiting event is dropped.</summary>
    public const int Capacity = 1000;

    private readonly Channel<ChatwootEvent> _events = Channel.CreateBounded<ChatwootEvent>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>Puts an event in the line.</summary>
    /// <param name="e">The event.</param>
    public void Enqueue(ChatwootEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        _events.Writer.TryWrite(e);
    }

    /// <summary>Reads the events in the order they came, until the host stops.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The events.</returns>
    public IAsyncEnumerable<ChatwootEvent> ReadAllAsync(CancellationToken cancellationToken)
        => _events.Reader.ReadAllAsync(cancellationToken);
}
