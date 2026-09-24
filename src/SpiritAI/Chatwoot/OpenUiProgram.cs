using System.Text.RegularExpressions;

namespace SpiritAI.Chatwoot;

/// <summary>
/// A read openui-lang program: follows names to what they hold and reads a call's arguments by
/// name, the way the widget's parser does.
/// </summary>
internal sealed partial class OpenUiProgram(Dictionary<string, OpenUiValue> statements)
{
    /// <summary>How far a chain of names, or a nest of values, is followed. A program that holds itself stops here.</summary>
    private const int MaxDepth = 32;

    public OpenUiValue? Root => statements.GetValueOrDefault("root");

    /// <summary>
    /// What a value stands for once names and <c>a.b</c> are followed. <see langword="null"/> for
    /// a name with no statement, which is also what <c>null</c>, <c>true</c> and <c>false</c> are.
    /// </summary>
    public OpenUiValue? Resolve(OpenUiValue? value)
    {
        for (var hops = 0; hops < MaxDepth; hops++)
        {
            switch (value)
            {
                case OpenUiValue.Name { Id: var id }:
                    value = statements.GetValueOrDefault(id);
                    break;

                case OpenUiValue.Member { Target: var target, Field: var field }:
                    value = Field(Resolve(target), field);
                    break;

                default:
                    return value;
            }
        }

        return null;
    }

    /// <summary>The argument called <paramref name="param"/>, resolved.</summary>
    public OpenUiValue? Arg(OpenUiValue.Call call, string param)
    {
        var index = OpenUiParams.IndexOf(call.Component, param);

        return index >= 0 && index < call.Arguments.Count ? Resolve(call.Arguments[index]) : null;
    }

    /// <summary>The argument called <paramref name="param"/> as text, when it is a string or a number.</summary>
    public string? Text(OpenUiValue.Call call, string param) => Scalar(Arg(call, param));

    /// <summary>The key <paramref name="key"/> of an object value, as text.</summary>
    public string? Text(OpenUiValue? value, string key) => Scalar(Resolve(Field(Resolve(value), key)));

    /// <summary>The items of a list value, resolved. Empty for anything else.</summary>
    public IEnumerable<OpenUiValue> Items(OpenUiValue? value)
        => Resolve(value) is OpenUiValue.List list
            ? list.Items.Select(Resolve).OfType<OpenUiValue>()
            : [];

    /// <summary>The items of the argument called <paramref name="param"/>.</summary>
    public IEnumerable<OpenUiValue> Items(OpenUiValue.Call call, string param) => Items(Arg(call, param));

    /// <summary>The calls of a list value, skipping anything that is not a call.</summary>
    public IEnumerable<OpenUiValue.Call> Calls(OpenUiValue.Call call, string param) => Items(call, param).OfType<OpenUiValue.Call>();

    /// <summary>
    /// The words a value shows, on one line: its strings and numbers, deep, in order. A string
    /// argument of a call that looks like a setting (<c>"large-heavy"</c>, <c>"info"</c>) is
    /// dropped, so a lower-case one-word label there is dropped too.
    /// </summary>
    public string Line(OpenUiValue? value) => string.Join(" ", Words(value, 0, false));

    /// <summary>The words a value shows, one per line.</summary>
    public string Lines(OpenUiValue? value) => string.Join("\n", Words(value, 0, false));

    /// <summary>The page an action opens, when its steps hold an <c>@OpenUrl</c>.</summary>
    public string? OpenedUrl(OpenUiValue? action)
        => Resolve(action) is OpenUiValue.Call { Component: "Action", Arguments: [var steps, ..] }
            ? Items(steps).OfType<OpenUiValue.Call>()
                .Where(step => step.Component == "@OpenUrl" && step.Arguments.Count > 0)
                .Select(step => Scalar(Resolve(step.Arguments[0])))
                .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
            : null;

    private IEnumerable<string> Words(OpenUiValue? value, int depth, bool argument)
    {
        if (depth >= MaxDepth)
        {
            return [];
        }

        return Resolve(value) switch
        {
            OpenUiValue.Text { Content: var text } when !(argument && IsSetting().IsMatch(text)) => [text.Trim()],
            OpenUiValue.Number { Written: var number } => [number],
            OpenUiValue.Call { Arguments: var items } => items.SelectMany(item => Words(item, depth + 1, true)),
            OpenUiValue.List { Items: var items } => items.SelectMany(item => Words(item, depth + 1, false)),
            OpenUiValue.Object { Entries: var entries } => entries.SelectMany(entry => Words(entry.Value, depth + 1, false)),
            _ => [],
        };
    }

    private static OpenUiValue? Field(OpenUiValue? value, string key)
        => value is OpenUiValue.Object { Entries: var entries }
            ? entries.FirstOrDefault(entry => entry.Key == key).Value
            : null;

    private static string? Scalar(OpenUiValue? value) => value switch
    {
        OpenUiValue.Text { Content: var text } => text,
        OpenUiValue.Number { Written: var number } => number,
        _ => null,
    };

    [GeneratedRegex(@"^[a-z0-9]+(?:[-_][a-z0-9]+)*$")]
    private static partial Regex IsSetting();
}
