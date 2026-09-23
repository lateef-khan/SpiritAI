using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Options;

using SpiritAI.Handoffs.RealTime;
using SpiritAI.RealTime;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Hears the visitor's typing signal on the socket and keeps it for Chatwoot. The chat is the one
/// the visitor was admitted to, never the one the payload names. Nothing is kept while the copy is
/// not configured, and a full line drops the oldest: typing is a hint.
/// </summary>
public sealed class ChatwootTypingQueue(IOptions<ChatwootOptions> options) : IRealTimeSignalListener
{
    /// <summary>How many signals may wait. Past it the oldest is dropped.</summary>
    public const int Capacity = 1000;

    private readonly Channel<VisitorTyping> _typing = Channel.CreateBounded<VisitorTyping>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <inheritdoc />
    public void Heard(RealTimeCaller caller, RealTimeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(signal);

        if (!options.Value.CopyEnabled
            || caller.Kind != HandoffAdmission.VisitorKind
            || signal.Name != ChatwootEventHandler.TypingSignal
            || signal.Group != HandoffGroups.Staff
            || OnOf(signal.Payload) is not { } on
            || caller.Groups.Select(HandoffGroups.ConversationOf).FirstOrDefault(c => c is not null) is not { } conversationId)
        {
            return;
        }

        _typing.Writer.TryWrite(new VisitorTyping(conversationId, on));
    }

    /// <summary>Reads the signals in the order they were heard, until the host stops.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The signals.</returns>
    public IAsyncEnumerable<VisitorTyping> ReadAllAsync(CancellationToken cancellationToken)
        => _typing.Reader.ReadAllAsync(cancellationToken);

    private static bool? OnOf(JsonElement payload)
        => payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("on", out var on)
            ? on.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;
}
