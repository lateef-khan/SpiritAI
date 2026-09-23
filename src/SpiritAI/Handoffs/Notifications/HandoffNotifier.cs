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
    public ValueTask ClaimedAsync(string conversationId, HandoffAssignee assignee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignee);

        return publisher.PublishAsync(HandoffGroups.ForConversation(conversationId), HandoffEvents.Claimed, new { callId = conversationId, assignee }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DoneAsync(string conversationId, CancellationToken cancellationToken)
        => publisher.PublishAsync(HandoffGroups.ForConversation(conversationId), HandoffEvents.Done, new { callId = conversationId }, cancellationToken);

    /// <inheritdoc />
    public ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return publisher.PublishAsync(HandoffGroups.ForConversation(message.ConversationId), HandoffEvents.MessageCreated, message, cancellationToken);
    }
}
