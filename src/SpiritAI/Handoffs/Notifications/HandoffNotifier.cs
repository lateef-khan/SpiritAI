using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.RealTime;

namespace SpiritAI.Handoffs.Notifications;

/// <summary>
/// The <see cref="IHandoffNotifier"/> that pushes through <see cref="IRealTimePublisher"/>: the
/// table of section 6.2 of the handoff spec, event by event, group by group.
/// </summary>
internal sealed class HandoffNotifier(IRealTimePublisher publisher) : IHandoffNotifier
{
    /// <inheritdoc />
    public ValueTask WaitingAsync(HandoffSummary handoff, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        return publisher.PublishAsync(HandoffGroups.Staff, HandoffEvents.Waiting, handoff, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask QueueAsync(string callId, int position, CancellationToken cancellationToken)
        => publisher.PublishAsync(
            HandoffGroups.ForCall(callId),
            HandoffEvents.Queue,
            new HandoffQueuePosition(callId, position),
            cancellationToken);

    /// <inheritdoc />
    public ValueTask ClaimedAsync(string callId, HandoffAssignee assignee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignee);

        return publisher.PublishAsync(StaffAndCall(callId), HandoffEvents.Claimed, new { callId, assignee }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DoneAsync(string callId, CancellationToken cancellationToken)
        => publisher.PublishAsync(StaffAndCall(callId), HandoffEvents.Done, new { callId }, cancellationToken);

    /// <inheritdoc />
    public ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return publisher.PublishAsync(StaffAndCall(message.CallId), HandoffEvents.MessageCreated, message, cancellationToken);
    }

    /// <summary>Every member of staff, and the visitor of one chat.</summary>
    private static string[] StaffAndCall(string callId) => [HandoffGroups.Staff, HandoffGroups.ForCall(callId)];
}
