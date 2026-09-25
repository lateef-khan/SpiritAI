using System.Text.Json;
using System.Threading.Channels;

namespace SpiritAI.GoTo;

/// <summary>
/// Holds the call events the webhook took in, until the reader handles them. The webhook answers
/// GoTo at once and never waits on the work.
/// </summary>
public sealed class GoToCallEventQueue
{
    /// <summary>The most events held. When it is full the oldest goes: a ring is stale in seconds.</summary>
    public const int Capacity = 256;

    private readonly Channel<JsonElement> events = Channel.CreateBounded<JsonElement>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    /// <summary>Puts one raw event on the queue.</summary>
    /// <param name="callEvent">The event body, cloned so it outlives the request.</param>
    public void Add(JsonElement callEvent) => events.Writer.TryWrite(callEvent);

    /// <summary>Gives each event as it arrives.</summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The events, oldest first.</returns>
    public IAsyncEnumerable<JsonElement> ReadAllAsync(CancellationToken cancellationToken)
        => events.Reader.ReadAllAsync(cancellationToken);
}
