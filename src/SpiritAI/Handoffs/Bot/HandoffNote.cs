using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// The private note a handoff leaves for staff: how to reach the person, saved on the contact or
/// not, and then the model's summary. The phone and email are always in it, because a value
/// Chatwoot refused is nowhere else.
/// </summary>
public static class HandoffNote
{
    /// <summary>Writes the note.</summary>
    /// <param name="phone">The phone line, from <see cref="Line"/>.</param>
    /// <param name="email">The email line, from <see cref="Line"/>.</param>
    /// <param name="summary">The model's summary.</param>
    /// <returns>The note's Markdown.</returns>
    public static string Write(string phone, string email, string summary)
        => $"Call back\n- Phone: {phone}\n- Email: {email}\n\n{summary.Trim()}";

    /// <summary>One way to reach the person, and what became of it.</summary>
    /// <param name="given">What the model passed.</param>
    /// <param name="shown">The checked value as staff read it, or <see langword="null"/> when it is not valid.</param>
    /// <param name="update">What Chatwoot did with it, or <see langword="null"/> when Chatwoot did not answer.</param>
    /// <returns>The line.</returns>
    public static string Line(string? given, string? shown, ChatwootContactUpdate? update)
    {
        if (string.IsNullOrWhiteSpace(given))
        {
            return "none given";
        }

        if (shown is null)
        {
            return $"{given.Trim()} (not valid)";
        }

        return update switch
        {
            { WasSaved: true } => $"{shown} (saved on the contact)",
            { Refusal: { } refusal } => $"{shown} (not saved: {refusal})",
            _ => $"{shown} (not saved: Chatwoot did not answer)",
        };
    }
}
