using System.Text.RegularExpressions;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Turns an AI message into Markdown staff can read in Chatwoot.
/// </summary>
public static partial class OpenUiMarkdown
{
    /// <summary>The Markdown of one AI message.</summary>
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
            var program = new OpenUiProgram(new OpenUiReader(message).Statements());

            return program.Root is { } root && new OpenUiBlocks(program).Write(root) is { } markdown && !string.IsNullOrWhiteSpace(markdown)
                ? markdown
                : message;
        }
        catch (FormatException)
        {
            return message;
        }
    }

    [GeneratedRegex(@"^\s*root\s*=", RegexOptions.Multiline)]
    private static partial Regex ProgramStart();
}
