namespace SpiritAI.Chatwoot;

/// <summary>One custom field that a Chatwoot contact can carry.</summary>
/// <param name="Key">The key the value is saved under in the contact's <c>custom_attributes</c>.</param>
/// <param name="Name">What staff see.</param>
/// <param name="Type">The display type, such as <c>text</c>, <c>number</c>, or <c>list</c>.</param>
/// <param name="Description">What the field holds.</param>
public sealed record ChatwootContactField(string Key, string Name, string Type, string Description);
