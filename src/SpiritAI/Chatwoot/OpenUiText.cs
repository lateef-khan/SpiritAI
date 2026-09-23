using System.Text.RegularExpressions;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Turns an AI message into words staff can read in Chatwoot. Markdown passes through. An
/// openui-lang program (<c>root = Card([...])</c>, the shape the widget draws) becomes the text in
/// it, in the order the card shows it, under <see cref="CardLine"/>.
/// </summary>
/// <remarks>
/// It reads only as much of the language as it needs: statements of the form <c>name = value</c>,
/// where a value is a call, a list, an object, a string, a name, or anything else, which is
/// skipped. Strings that look like a setting (<c>"large-heavy"</c>, <c>"info"</c>, a field name)
/// are dropped, so a lower-case one-word label is dropped too. A program it cannot read goes to
/// Chatwoot as it is, so nothing is ever lost.
/// </remarks>
public static partial class OpenUiText
{
    /// <summary>The first line of a card's text.</summary>
    public const string CardLine = "[Card shown to the customer]";

    private const string Root = "root";

    /// <summary>The staff-readable text of one AI message.</summary>
    /// <param name="message">The message as AgentCore stored it.</param>
    /// <returns>The text to post.</returns>
    public static string ForStaff(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!ProgramStart().IsMatch(message))
        {
            return message;
        }

        try
        {
            var statements = new OpenUiReader(message).Statements();

            if (!statements.ContainsKey(Root))
            {
                return message;
            }

            var lines = new List<string>();
            Collect(new OpenUiValue.Name(Root), statements, lines, new HashSet<string>(StringComparer.Ordinal));

            return lines.Count == 0 ? CardLine : CardLine + "\n" + string.Join("\n", lines);
        }
        catch (FormatException)
        {
            return message;
        }
    }

    private static void Collect(OpenUiValue value, Dictionary<string, OpenUiValue> statements, List<string> lines, HashSet<string> visited)
    {
        switch (value)
        {
            case OpenUiValue.Text { Content: var text } when !IsSetting().IsMatch(text):
                lines.Add(text.Trim());
                break;

            case OpenUiValue.Name { Id: var id } when visited.Add(id) && statements.TryGetValue(id, out var definition):
                Collect(definition, statements, lines, visited);
                break;

            case OpenUiValue.Call { Arguments: var items }:
                foreach (var item in items)
                {
                    Collect(item, statements, lines, visited);
                }

                break;

            case OpenUiValue.List { Items: var items }:
                foreach (var item in items)
                {
                    Collect(item, statements, lines, visited);
                }

                break;
        }
    }

    [GeneratedRegex(@"^\s*root\s*=", RegexOptions.Multiline)]
    private static partial Regex ProgramStart();

    [GeneratedRegex(@"^[a-z0-9]+(?:[-_][a-z0-9]+)*$")]
    private static partial Regex IsSetting();
}
