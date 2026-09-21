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
    public ValueTask QueueAsync(string conversationId, int position, CancellationToken cancellationToken)
        => publisher.PublishAsync(
            HandoffGroups.ForConversation(conversationId),
            HandoffEvents.Queue,
            new HandoffQueuePosition(conversationId, position),
            cancellationToken);

    /// <inheritdoc />
    public ValueTask ClaimedAsync(string conversationId, HandoffAssignee assignee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignee);

        return publisher.PublishAsync(StaffAndConversation(conversationId), HandoffEvents.Claimed, new { callId = conversationId, assignee }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DoneAsync(string conversationId, CancellationToken cancellationToken)
        => publisher.PublishAsync(StaffAndConversation(conversationId), HandoffEvents.Done, new { callId = conversationId }, cancellationToken);

    /// <inheritdoc />
    public ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return publisher.PublishAsync(StaffAndConversation(message.ConversationId), HandoffEvents.MessageCreated, message, cancellationToken);
    }

    /// <summary>Every member of staff, and the visitor of one chat.</summary>
    private static string[] StaffAndConversation(string conversationId) => [HandoffGroups.Staff, HandoffGroups.ForConversation(conversationId)];
}
