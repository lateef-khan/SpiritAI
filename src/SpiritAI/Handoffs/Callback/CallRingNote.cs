using System.Globalization;

using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Callback;

/// <summary>
/// The private note that tells staff a visitor who asked for a person is on the phone. Chatwoot
/// reads each <c>(mention://user/{id}/{name})</c> and <c>(mention://team/{id}/{name})</c> in a
/// private note and notifies the people it names.
/// </summary>
public static class CallRingNote
{
    /// <summary>Writes the note.</summary>
    /// <param name="outbound">Whether staff placed the call.</param>
    /// <param name="number">The visitor's number, as people read it.</param>
    /// <param name="code">The visitor's code, the conversation's display id.</param>
    /// <param name="mentions">What <see cref="Mention(ChatwootAgent)"/> and <see cref="Mention(ChatwootTeam)"/> wrote.</param>
    /// <returns>The note.</returns>
    public static string Write(bool outbound, string number, int code, IReadOnlyList<string> mentions)
    {
        var line = outbound
            ? string.Create(CultureInfo.InvariantCulture, $"📞 Calling {number} now — code {code}.")
            : string.Create(CultureInfo.InvariantCulture, $"📞 {number} is calling now — code {code}.");

        return mentions.Count == 0 ? line : $"{line} {string.Join(' ', mentions)}";
    }

    /// <summary>A mention of one member of staff.</summary>
    /// <param name="agent">Who to notify.</param>
    /// <returns>The mention, as Chatwoot's own editor writes it.</returns>
    public static string Mention(ChatwootAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return string.Create(CultureInfo.InvariantCulture, $"[@{agent.Name}](mention://user/{agent.Id}/{Uri.EscapeDataString(agent.Name)})");
    }

    /// <summary>A mention of a team, which notifies each member.</summary>
    /// <param name="team">Who to notify.</param>
    /// <returns>The mention, as Chatwoot's own editor writes it.</returns>
    public static string Mention(ChatwootTeam team)
    {
        ArgumentNullException.ThrowIfNull(team);

        return string.Create(CultureInfo.InvariantCulture, $"[@{team.Name}](mention://team/{team.Id}/{Uri.EscapeDataString(team.Name)})");
    }
}
