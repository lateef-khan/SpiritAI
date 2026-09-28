namespace SpiritAI.Chatwoot;

/// <summary>
/// The name a member of staff goes by in front of a visitor: the display name they set, or else
/// the first word of their account name. The widget applies the same rule in
/// <c>src/web/src/lib/chatwoot/staffName.ts</c>.
/// </summary>
public static class ChatwootStaffName
{
    /// <summary>Reads the name.</summary>
    /// <param name="name">The account name, Chatwoot's <c>name</c>.</param>
    /// <param name="availableName">
    /// Chatwoot's <c>available_name</c>: the display name when one is set, else the account name. It
    /// differs from <paramref name="name"/> only when a display name is set.
    /// </param>
    /// <returns>The name, or <see langword="null"/> when Chatwoot names nobody.</returns>
    public static string? Of(string? name, string? availableName)
    {
        var display = availableName?.Trim();

        if (!string.IsNullOrEmpty(display) && display != name?.Trim())
        {
            return display;
        }

        var words = name?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return words is { Length: > 0 } ? words[0] : null;
    }
}
