using System.Text.Json;
using System.Threading.Channels;
using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

using SpiritAI.Handoffs.RealTime;
using SpiritAI.RealTime;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Hears the visitor's typing signal on the socket and keeps it for Chatwoot.
/// </summary>
public sealed class ChatwootTypingQueue(IOptions<ChatwootOptions> options) : IRealTimeSignalListener, IDisposable
{
    /// <summary>How many signals may wait. Past it the oldest is dropped.</summary>
    public const int Capacity = 1000;

    /// <summary>How many signals one chat may send at once, before the one-a-second limit starts.</summary>
    public const int Burst = 4;

    private readonly PartitionedRateLimiter<string> _perChat = PartitionedRateLimiter.Create<string, string>(
        conversationId => RateLimitPartition.GetTokenBucketLimiter(conversationId, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = Burst,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

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

        using var lease = _perChat.AttemptAcquire(conversationId);

        if (lease.IsAcquired)
        {
            _typing.Writer.TryWrite(new VisitorTyping(conversationId, on));
        }
    }

    /// <summary>Reads the signals in the order they were heard, until the host stops.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The signals.</returns>
    public IAsyncEnumerable<VisitorTyping> ReadAllAsync(CancellationToken cancellationToken)
        => _typing.Reader.ReadAllAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _perChat.Dispose();

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
