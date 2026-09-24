namespace SpiritAI.Chatwoot;

/// <summary>One value of an openui-lang program, as far as <see cref="OpenUiMarkdown"/> needs it.</summary>
internal abstract record OpenUiValue
{
    public sealed record Text(string Content) : OpenUiValue;

    /// <summary>A number, kept as written, so <c>2.5</c> shows as <c>2.5</c>.</summary>
    public sealed record Number(string Written) : OpenUiValue;

    /// <summary>A reference to a statement, or <c>true</c>, <c>false</c>, <c>null</c>.</summary>
    public sealed record Name(string Id) : OpenUiValue;

    /// <summary><c>target.field</c>.</summary>
    public sealed record Member(OpenUiValue Target, string Field) : OpenUiValue;

    /// <summary>A component call, or an action step such as <c>@OpenUrl(...)</c>, whose name keeps its <c>@</c>.</summary>
    public sealed record Call(string Component, IReadOnlyList<OpenUiValue> Arguments) : OpenUiValue;

    public sealed record List(IReadOnlyList<OpenUiValue> Items) : OpenUiValue;

    public sealed record Object(IReadOnlyList<KeyValuePair<string, OpenUiValue>> Entries) : OpenUiValue;

    /// <summary>Anything else: <c>$state</c>, an operator, a ternary. The copy cannot know its value.</summary>
    public sealed record Other : OpenUiValue;
}
