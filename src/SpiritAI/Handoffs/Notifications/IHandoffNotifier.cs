using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Notifications;

/// <summary>
/// The pushes of section 6.2 of the spec, server to client. The routes call these after a state
/// change has committed; what carries them to a browser is behind this port.
/// </summary>
public interface IHandoffNotifier
{
    /// <summary>A chat joined the queue. To staff.</summary>
    /// <param name="handoff">The new row, with its position and first line.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask WaitingAsync(HandoffSummary handoff, CancellationToken cancellationToken);

    /// <summary>A waiting chat's place in the line moved. To that chat.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="position">Where it stands now, one for the front.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask QueueAsync(string callId, int position, CancellationToken cancellationToken);

    /// <summary>A member of staff took a chat. To staff and to that chat.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="assignee">Who took it.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask ClaimedAsync(string callId, HandoffAssignee assignee, CancellationToken cancellationToken);

    /// <summary>A chat went back to the bot. To staff and to that chat.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask DoneAsync(string callId, CancellationToken cancellationToken);

    /// <summary>A message landed in a chat of the human phase. To staff and to that chat.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken);

    /// <summary>How many staff are on a socket. To staff and to every chat.</summary>
    /// <param name="staffOnline">The count.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask PresenceAsync(int staffOnline, CancellationToken cancellationToken);
}
