using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// The bot's door into the queue, section 9.4 of the handoff spec: what the <c>RequestHuman</c>
/// binding in <c>spirit.yaml</c> runs.
/// </summary>
public sealed class RequestHumanTool(HandoffDesk desk)
{
    /// <summary>What the model is told once a person has been asked and somebody is here to see it.</summary>
    public const string AskedNote = "A person has been asked to join.";

    /// <summary>What the model is told once a person has been asked and nobody is here to see it.</summary>
    public const string NobodyFreeNote = "Nobody is free right now. Offer to take an email.";

    /// <summary>Asks for a person on the chat of the turn under way.</summary>
    /// <param name="callId">The chat, as AgentCore names it to the binding.</param>
    /// <param name="reason">Why, in the person's own words.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>The place in line, and how many staff are online.</returns>
    public async Task<RequestHumanAnswer> AskAsync(string callId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        var staffOnline = await desk.StaffOnlineAsync(cancellationToken).ConfigureAwait(false);

        var asked = await desk.AskAsync(callId, HandoffAskedBy.Bot, reason, cancellationToken).ConfigureAwait(false);

        return new RequestHumanAnswer(
            asked.Ticket.Position,
            staffOnline,
            staffOnline > 0 ? AskedNote : NobodyFreeNote);
    }
}
