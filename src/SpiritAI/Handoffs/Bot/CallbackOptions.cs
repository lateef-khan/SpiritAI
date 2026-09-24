namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// What the visitor is promised about the call back, bound from the <see cref="SectionName"/> section.
/// </summary>
public sealed class CallbackOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Callback";

    /// <summary>
    /// When staff call back, as the owner words it, for example <c>within 2 hours, Mon-Fri 9-5</c>.
    /// It follows "We will call you at +1 201-555-0123". Empty says nothing about timing.
    /// </summary>
    public string? Promise { get; set; }
}
