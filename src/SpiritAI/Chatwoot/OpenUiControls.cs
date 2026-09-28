namespace SpiritAI.Chatwoot;

/// <summary>
/// The parts of a card the visitor acts on, or reads as a list: steps, lists, follow-up chips,
/// buttons and forms. Staff cannot press them in Chatwoot, so each is labelled for what it is.
/// </summary>
internal static class OpenUiControls
{
    /// <summary><c>Steps([StepsItem(title, details), ...])</c>.</summary>
    public static string? Steps(OpenUiProgram program, OpenUiValue.Call steps)
        => Join(program.Calls(steps, "items")
            .Select((item, i) => $"{i + 1}. {Entry(program.Text(item, "title"), program.Text(item, "details"))}"));

    /// <summary><c>ListBlock([ListItem(title, subtitle, image, actionLabel, action), ...], variant)</c>.</summary>
    public static string? List(OpenUiProgram program, OpenUiValue.Call list)
    {
        var numbered = program.Text(list, "variant") == "number";

        return Join(program.Calls(list, "items").Select((item, i) =>
        {
            var title = program.Text(item, "title");
            var url = program.OpenedUrl(program.Arg(item, "action"));
            var entry = Entry(title is not null && url is not null ? MarkdownText.Link(title, url) : title, program.Text(item, "subtitle"));

            return (numbered ? $"{i + 1}. " : "- ") + entry;
        }));
    }

    /// <summary><c>FollowUpBlock([FollowUpItem(text), ...])</c>: the chips the visitor can send next.</summary>
    public static string? FollowUps(OpenUiProgram program, OpenUiValue.Call block)
        => Join(program.Calls(block, "items").Select(item => program.Text(item, "text")).OfType<string>().Select(text => "- " + text))
            is { } lines
            ? "_Suggested replies:_\n" + lines
            : null;

    /// <summary>A <c>Buttons(...)</c> group or one <c>Button(...)</c>. A button that opens a page is a link.</summary>
    public static string? Buttons(OpenUiProgram program, OpenUiValue.Call buttons)
    {
        IEnumerable<OpenUiValue.Call> each = buttons.Component == "Button" ? [buttons] : program.Calls(buttons, "buttons");

        var labels = each
            .Select(button => (Label: program.Text(button, "label"), Url: program.OpenedUrl(program.Arg(button, "action"))))
            .Where(button => !string.IsNullOrWhiteSpace(button.Label))
            .Select(button => button.Url is null ? MarkdownText.Code(button.Label!) : MarkdownText.Link(button.Label!, button.Url))
            .ToList();

        return labels.Count == 0 ? null : "_Buttons:_ " + string.Join(" · ", labels);
    }

    /// <summary><c>Form(name, buttons, [FormControl(label, input, hint), ...])</c>: what the form asks, then its buttons.</summary>
    public static string? Form(OpenUiProgram program, OpenUiValue.Call form)
    {
        var fields = Join(program.Calls(form, "fields").Select(field =>
        {
            var line = $"- **{program.Text(field, "label")}**: {Kind(program, program.Arg(field, "input"))}";

            return program.Text(field, "hint") is { } hint ? $"{line} — {hint}" : line;
        }));

        var buttons = program.Arg(form, "buttons") is OpenUiValue.Call call ? Buttons(program, call) : null;

        return fields is null && buttons is null
            ? null
            : string.Join("\n\n", new[] { fields is null ? null : "_Form:_\n" + fields, buttons }.OfType<string>());
    }

    /// <summary>What one form input takes: its kind, or the options it offers.</summary>
    private static string Kind(OpenUiProgram program, OpenUiValue? input) => input switch
    {
        OpenUiValue.Call { Component: "Input" } call => program.Text(call, "type") ?? "text",
        OpenUiValue.Call { Component: "TextArea" } => "text",
        OpenUiValue.Call { Component: "DatePicker" } call => program.Text(call, "mode") == "range" ? "date range" : "date",
        OpenUiValue.Call { Component: "Slider" } call => $"{program.Text(call, "min")}–{program.Text(call, "max")}",
        OpenUiValue.Call { Component: "Select" or "RadioGroup" or "CheckBoxGroup" or "SwitchGroup" } call
            => string.Join(" / ", program.Calls(call, "items").Select(item => program.Text(item, "label")).OfType<string>()),
        _ => program.Line(input),
    };

    /// <summary><c>**title** — detail</c>, leaving out whichever is missing.</summary>
    private static string Entry(string? title, string? detail)
        => string.Join(" — ", new[] { title is null ? null : $"**{title}**", detail }.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string? Join(IEnumerable<string> lines)
    {
        var all = lines.ToList();

        return all.Count == 0 ? null : string.Join("\n", all);
    }
}
