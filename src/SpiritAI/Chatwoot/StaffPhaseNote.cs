using System.Text;

using Microsoft.Extensions.AI;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The part of a chat a person from the support team had, as one system message for the AI.
/// </summary>
/// <remarks>
/// The model reads a staff line in an assistant message as its own words, and disowns a promise it
/// never made. One captioned report on neither side of the chat is the shape OpenAI's Agents SDK
/// gives a previous agent's turns (<c>nest_handoff_history</c>); see
/// <c>docs/superpowers/specs/2026-09-24-staff-messages-in-ai-context-research.md</c>.
/// </remarks>
public static class StaffPhaseNote
{
    /// <summary>The line the note opens with.</summary>
    public const string Opening =
        "While you were away, a person from our support team had this chat with the customer. You did not write these lines.";

    /// <summary>The line the note closes with.</summary>
    public const string Closing =
        "What the support team said or promised, the company said and promised. Do not deny it or change it.";

    /// <summary>Who a staff line is from when Chatwoot names nobody.</summary>
    public const string UnnamedStaff = "Staff";

    /// <summary>Writes the note.</summary>
    /// <param name="phase">The staff part, oldest first: staff messages and the customer's answers between them.</param>
    /// <returns>The system message.</returns>
    public static ChatMessage For(IEnumerable<ChatwootMessage> phase)
    {
        ArgumentNullException.ThrowIfNull(phase);

        var text = new StringBuilder(Opening).Append('\n');

        foreach (var message in phase)
        {
            var who = message.SenderType == "contact"
                ? "Customer"
                : $"{message.SenderName ?? UnnamedStaff} (support team)";

            text.Append(who).Append(": ").Append(message.Content).Append('\n');
        }

        return new ChatMessage(ChatRole.System, text.Append(Closing).ToString());
    }
}
