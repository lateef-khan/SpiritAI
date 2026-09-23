using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Notifications;

/// <summary>
/// The handoff pushes of section 6.2 of the spec, server to client. The routes conversation these after a
/// state change has committed; what carries them to a browser is behind this port. Presence is
/// not here: the hub counts every kind of caller itself.
/// </summary>
public interface IHandoffNotifier
{
    /// <summary>A member of staff took a chat. To that chat.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="assignee">Who took it.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask ClaimedAsync(string conversationId, HandoffAssignee assignee, CancellationToken cancellationToken);

    /// <summary>A chat went back to the bot. To that chat.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask DoneAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>A message landed in a chat of the human phase. To that chat.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken);
}
