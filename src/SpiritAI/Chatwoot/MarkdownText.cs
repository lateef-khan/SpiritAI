using System.Text;

namespace SpiritAI.Chatwoot;

/// <summary>The small pieces of Markdown <see cref="OpenUiMarkdown"/> writes, as Chatwoot's markdown-it reads them.</summary>
internal static class MarkdownText
{
    /// <summary>A GFM table. A row shorter than the header is filled with empty cells.</summary>
    public static string Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var table = new StringBuilder();

        Row(table, headers);
        Row(table, headers.Select(_ => "---").ToList());

        foreach (var row in rows)
        {
            Row(table, headers.Select((_, i) => i < row.Count ? row[i] : "").ToList());
        }

        return table.ToString().TrimEnd('\n');
    }

    /// <summary>A fenced code block, fenced longer than any backtick run in the code.</summary>
    public static string Fence(string? language, string code)
    {
        var fence = new string('`', Math.Max(3, LongestRun(code, '`') + 1));

        return $"{fence}{language}\n{code}\n{fence}";
    }

    /// <summary>An inline code span.</summary>
    public static string Code(string text) => text.Contains('`', StringComparison.Ordinal) ? $"`` {text} ``" : $"`{text}`";

    public static string Link(string label, string url) => $"[{label}]({Url(url)})";

    /// <summary>An image, or its alt text in italics when it has no address.</summary>
    public static string? Image(string? alt, string? src)
        => string.IsNullOrWhiteSpace(src)
            ? string.IsNullOrWhiteSpace(alt) ? null : $"_{alt}_"
            : $"![{alt}]({Url(src)})";

    private static void Row(StringBuilder table, IReadOnlyList<string> cells)
        => table.Append("| ").AppendJoin(" | ", cells.Select(Cell)).Append(" |\n");

    private static string Cell(string text)
        => text.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ');

    /// <summary>An address markdown-it reads whole: one with a space or a bracket goes in angle brackets.</summary>
    private static string Url(string url) => url.IndexOfAny([' ', '(', ')']) >= 0 ? $"<{url}>" : url;

    private static int LongestRun(string text, char c)
    {
        int longest = 0, run = 0;

        foreach (var x in text)
        {
            run = x == c ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }
}
