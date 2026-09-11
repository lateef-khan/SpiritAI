using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// The bot's door into the queue, section 9.4 of the handoff spec: what the <c>RequestHuman</c>
/// binding in <c>spirit.yaml</c> runs.
/// </summary>
public sealed class RequestHumanTool(ICurrentCall current, HandoffDesk desk)
{
    /// <summary>What the model is told when this host cannot say which chat the turn belongs to.</summary>
    public const string NoCallNote =
        "This chat cannot be handed to a person right now. Tell the person to tap 'Talk to a person'.";

    /// <summary>What the model is told once a person has been asked and somebody is here to see it.</summary>
    public const string AskedNote = "A person has been asked to join.";

    /// <summary>What the model is told once a person has been asked and nobody is here to see it.</summary>
    public const string NobodyFreeNote = "Nobody is free right now. Offer to take an email.";

    /// <summary>Asks for a person on the chat of the turn under way.</summary>
    /// <param name="reason">Why, in the person's own words.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>Whether a person was asked, the place in line, and how many staff are online.</returns>
    public async Task<RequestHumanAnswer> AskAsync(string reason, CancellationToken cancellationToken)
    {
        var staffOnline = await desk.StaffOnlineAsync(cancellationToken).ConfigureAwait(false);

        if (current.CallId is not { } callId)
        {
            return new RequestHumanAnswer(Ok: false, Position: null, staffOnline, NoCallNote);
        }

        var asked = await desk.AskAsync(callId, HandoffAskedBy.Bot, reason, cancellationToken).ConfigureAwait(false);

        return new RequestHumanAnswer(
            Ok: true,
            asked.Ticket.Position,
            staffOnline,
            staffOnline > 0 ? AskedNote : NobodyFreeNote);
    }
}
