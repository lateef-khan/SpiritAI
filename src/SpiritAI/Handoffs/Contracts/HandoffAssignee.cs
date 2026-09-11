namespace SpiritAI.Handoffs.Contracts;

/// <summary>
/// The member of staff holding a chat.
/// </summary>
/// <param name="Key">Their caller key, the one their claim was filed under.</param>
/// <param name="Name">The name the visitor sees.</param>
public sealed record HandoffAssignee(string Key, string Name);
