using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Notifications;

namespace SpiritAI.Tests.Handoffs;

/// <summary>An <see cref="IHandoffNotifier"/> that keeps every push, named the way the wire names them.</summary>
internal sealed class RecordingHandoffNotifier : IHandoffNotifier
{
    /// <summary>Every push, in order: the event name and what it carried.</summary>
    public List<(string Event, object Payload)> Pushed { get; } = [];

    /// <summary>The names alone, for a test that cares about the order and not the payloads.</summary>
    public IEnumerable<string> Events => Pushed.Select(push => push.Event);

    public ValueTask WaitingAsync(HandoffSummary handoff, CancellationToken cancellationToken)
        => Record("handoff.waiting", handoff);

    public ValueTask QueueAsync(string callId, int position, CancellationToken cancellationToken)
        => Record("handoff.queue", (callId, position));

    public ValueTask ClaimedAsync(string callId, HandoffAssignee assignee, CancellationToken cancellationToken)
        => Record("handoff.claimed", (callId, assignee));

    public ValueTask DoneAsync(string callId, CancellationToken cancellationToken)
        => Record("handoff.done", callId);

    public ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken)
        => Record("message.created", message);

    public ValueTask PresenceAsync(int staffOnline, CancellationToken cancellationToken)
        => Record("presence", staffOnline);

    private ValueTask Record(string name, object payload)
    {
        Pushed.Add((name, payload));
        return ValueTask.CompletedTask;
    }
}
