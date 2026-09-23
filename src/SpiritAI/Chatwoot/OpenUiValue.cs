namespace SpiritAI.Chatwoot;

/// <summary>One value of an openui-lang program, as far as <see cref="OpenUiText"/> needs it.</summary>
internal abstract record OpenUiValue
{
    public sealed record Text(string Content) : OpenUiValue;

    public sealed record Name(string Id) : OpenUiValue;

    public sealed record Call(IReadOnlyList<OpenUiValue> Arguments) : OpenUiValue;

    public sealed record List(IReadOnlyList<OpenUiValue> Items) : OpenUiValue;

    public sealed record Other : OpenUiValue;
}
