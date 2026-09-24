using SpiritAI.Handoffs.Model;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Tells Chatwoot that a chat wants a person: a private note with why and where to reach the
/// visitor, then the conversation handed to staff. Each handoff is told once; a phone number the
/// visitor gives later gets a note of its own.
/// </summary>
public sealed class ChatwootHandoffNotice(ChatwootClient chatwoot)
{
    /// <summary>Tells Chatwoot what it has not heard yet about the chat's open handoff, and marks it told on the link.</summary>
    /// <param name="link">The chat's link. Updated; the caller saves it.</param>
    /// <param name="open">The chat's open handoff.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task TellAsync(ChatwootLink link, Handoff open, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(open);

        if (link.AnnouncedHandoffId != open.Id)
        {
            await chatwoot.PostNoteAsync(link.ChatwootConversationId, AskedNote(open), cancellationToken).ConfigureAwait(false);
            await chatwoot.HandToStaffAsync(link.ChatwootConversationId, cancellationToken).ConfigureAwait(false);

            link.AnnouncedHandoffId = open.Id;
            link.NotedPhone = open.Phone;
            return;
        }

        if (open.Phone is { } phone && phone != link.NotedPhone)
        {
            await chatwoot.PostNoteAsync(link.ChatwootConversationId, PhoneLine(phone, open.Id), cancellationToken).ConfigureAwait(false);

            link.NotedPhone = phone;
        }
    }

    /// <summary>The note staff read first: who asked, why, and the phone number when there is one.</summary>
    public static string AskedNote(Handoff open)
    {
        ArgumentNullException.ThrowIfNull(open);

        var lines = new List<string>
        {
            open.AskedBy == HandoffAskedBy.Visitor
                ? "The customer pressed the button to talk to a person."
                : "The AI asked for a person on this chat.",
        };

        if (!string.IsNullOrWhiteSpace(open.Reason))
        {
            lines.Add("Reason: " + open.Reason.Trim());
        }

        if (open.Phone is { } phone)
        {
            lines.Add(PhoneLine(phone, open.Id));
        }

        return string.Join("\n", lines);
    }

    private static string PhoneLine(string phone, long code)
        => $"The customer's phone: {VisitorPhone.Display(phone)} — code {code}";
}
