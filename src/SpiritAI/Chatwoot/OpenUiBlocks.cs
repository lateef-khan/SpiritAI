namespace SpiritAI.Chatwoot;

/// <summary>
/// Walks a card from <c>root</c> and writes each component as a Markdown block. Blocks are
/// joined by a blank line.
/// </summary>
internal sealed class OpenUiBlocks(OpenUiProgram program)
{
    /// <summary>How deep blocks nest before the walk stops. A program that holds itself stops here.</summary>
    private const int MaxDepth = 20;

    private int _depth;

    public string? Write(OpenUiValue value) => Block(value);

    private string? Block(OpenUiValue? value)
    {
        if (_depth >= MaxDepth)
        {
            return null;
        }

        _depth++;

        try
        {
            return program.Resolve(value) switch
            {
                OpenUiValue.Text { Content: var text } => text,
                OpenUiValue.Call call => Component(call),
                OpenUiValue.List list => Blocks(list),
                _ => null,
            };
        }
        finally
        {
            _depth--;
        }
    }

    private string? Blocks(OpenUiValue? list) => Join(program.Items(list).Select(Block), "\n\n");

    private string? Component(OpenUiValue.Call call) => call.Component switch
    {
        "Card" or "Stack" => Blocks(program.Arg(call, "children")),
        "CardHeader" => Join([Prefix("### ", program.Text(call, "title")), Italic(program.Text(call, "subtitle"))], "\n"),
        "TextContent" => TextContent(program.Text(call, "text"), program.Text(call, "size")),
        "MarkDownRenderer" => program.Text(call, "textMarkdown"),
        "Label" => program.Text(call, "text"),
        "Callout" or "TextCallout" => Callout(call),
        "CodeBlock" => program.Text(call, "codeString") is { } code ? MarkdownText.Fence(program.Text(call, "language"), code) : null,
        "Image" or "ImageBlock" => MarkdownText.Image(program.Text(call, "alt"), program.Text(call, "src")),
        "ImageGallery" => Gallery(call),
        "Separator" => "---",
        "Table" => OpenUiData.Table(program, call),
        _ when OpenUiData.IsChart(call.Component) => OpenUiData.Chart(program, call),
        "TagBlock" => Join(program.Items(call, "tags").Select(program.Line).Select(MarkdownText.Code), " "),
        "Tag" => program.Text(call, "text") is { } tag ? MarkdownText.Code(tag) : null,
        "Steps" => OpenUiControls.Steps(program, call),
        "ListBlock" => OpenUiControls.List(program, call),
        "FollowUpBlock" => OpenUiControls.FollowUps(program, call),
        "Buttons" or "Button" => OpenUiControls.Buttons(program, call),
        "Form" => OpenUiControls.Form(program, call),
        "SectionBlock" => Sections(program.Items(call, "sections")),
        "Accordion" or "Tabs" => Sections(program.Items(call, "items")),
        "SectionItem" or "AccordionItem" or "TabItem" => Sections([call]),
        "Carousel" => Join(program.Items(call, "children").Select(Blocks), "\n\n---\n\n"),
        "Modal" => Join([Prefix("#### ", program.Text(call, "title")), Blocks(program.Arg(call, "children"))], "\n\n"),
        _ => program.Lines(call),
    };

    /// <summary>A heavy size on one plain line is a title, so it is bold. Anything else is already Markdown.</summary>
    private static string? TextContent(string? text, string? size)
        => text is not null
            && size?.EndsWith("-heavy", StringComparison.Ordinal) == true
            && !text.Contains('\n', StringComparison.Ordinal)
            && !text.Contains("**", StringComparison.Ordinal)
            && !text.StartsWith('#')
                ? $"**{text.Trim()}**"
                : text;

    /// <summary>A quote, its first line labelled by the variant.</summary>
    private string? Callout(OpenUiValue.Call call)
    {
        var label = program.Text(call, "variant") switch
        {
            "info" => "Note",
            "warning" => "Warning",
            "error" or "danger" => "Error",
            "success" => "Done",
            _ => null,
        };

        var title = program.Text(call, "title");
        var heading = (label, title) switch
        {
            (null, null) => null,
            (null, _) => $"**{title}**",
            (_, null) => $"**{label}**",
            _ => $"**{label}: {title}**",
        };

        var lines = Join([heading, program.Text(call, "description")], "\n");

        return lines is null ? null : string.Join("\n", lines.Split('\n').Select(line => "> " + line));
    }

    private string? Gallery(OpenUiValue.Call call)
        => Join(program.Items(call, "images").Select(image => Join(
            [
                MarkdownText.Image(program.Text(image, "alt"), program.Text(image, "src")),
                Italic(program.Text(image, "details")),
            ],
            "\n")), "\n\n");

    /// <summary>Tabs, accordion items and sections: each trigger is a heading over its content.</summary>
    private string? Sections(IEnumerable<OpenUiValue> items)
        => Join(items.OfType<OpenUiValue.Call>().Select(item => Join(
            [
                Prefix("#### ", program.Text(item, "trigger")),
                Blocks(program.Arg(item, "content")),
            ],
            "\n\n")), "\n\n");

    private static string? Prefix(string prefix, string? text) => string.IsNullOrWhiteSpace(text) ? null : prefix + text;

    private static string? Italic(string? text) => string.IsNullOrWhiteSpace(text) ? null : $"_{text}_";

    private static string? Join(IEnumerable<string?> parts, string separator)
    {
        var kept = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();

        return kept.Count == 0 ? null : string.Join(separator, kept);
    }
}
