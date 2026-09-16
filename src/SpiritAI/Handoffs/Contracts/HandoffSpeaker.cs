using System.Text.Json.Serialization;

namespace SpiritAI.Handoffs.Contracts;

/// <summary>
/// Who produced a message, when that is not simply "the agent". The browser's <c>Speaker</c> in
/// <c>transport.ts</c>, spelled the same on the wire.
/// </summary>
/// <param name="Kind">
/// <c>human</c> for a real person, <c>system</c> for the host speaking for itself. <c>agent</c>
/// exists on the wire for the model; nothing here writes it.
/// </param>
/// <param name="Name">What is drawn above the message.</param>
/// <param name="Detail">A role or team, drawn under the name. Left off the wire when there is none.</param>
public sealed record HandoffSpeaker(
    string Kind,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Detail)
{
    /// <summary>The kind a member of staff's reply carries.</summary>
    public const string HumanKind = "human";

    /// <summary>The kind a line the host wrote about the chat carries.</summary>
    public const string SystemKind = "system";

    /// <summary>The name the host signs its own lines with.</summary>
    public const string SystemName = "Spirit";

    /// <summary>A member of staff.</summary>
    /// <param name="name">The name the visitor sees.</param>
    /// <param name="detail">Their team, or anything else worth showing under the name.</param>
    /// <returns>The speaker.</returns>
    public static HandoffSpeaker Human(string name, string? detail) => new(HumanKind, name, detail);

    /// <summary>The host, saying who joined or left.</summary>
    /// <returns>The speaker.</returns>
    public static HandoffSpeaker System() => new(SystemKind, SystemName, Detail: null);
}
