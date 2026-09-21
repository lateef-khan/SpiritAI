using System.Net.Mail;

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
    public const string AskedNote = "A person has been asked to join, and someone is online.";

    /// <summary>What the model is told once a person has been asked and nobody is here to see it.</summary>
    public const string NobodyFreeNote =
        "A person has been asked to join. Nobody is online right now; a reply will reach them by email.";

    /// <summary>What the model is told when the email it passed cannot be delivered to.</summary>
    public const string BadEmailNote = " The email given does not look like an address; ask for it again.";

    /// <summary>Asks for a person on the chat of the turn under way.</summary>
    /// <param name="conversationId">The chat, as AgentCore names it to the binding.</param>
    /// <param name="reason">Why, in the person's own words.</param>
    /// <param name="email">Where a reply goes when they are not there to read it, when they gave one.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>One sentence for the model to pass on.</returns>
    public async Task<RequestHumanAnswer> AskAsync(string conversationId, string reason, string? email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var staffOnline = await desk.StaffOnlineAsync(cancellationToken).ConfigureAwait(false);

        await desk.AskAsync(conversationId, HandoffAskedBy.Bot, reason, cancellationToken).ConfigureAwait(false);

        var note = staffOnline > 0 ? AskedNote : NobodyFreeNote;

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (MailAddress.TryCreate(email.Trim(), out var address))
            {
                await desk.SetEmailAsync(conversationId, address.Address, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                note += BadEmailNote;
            }
        }

        return new RequestHumanAnswer(note);
    }
}
