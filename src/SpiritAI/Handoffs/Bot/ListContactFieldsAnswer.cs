using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>What <c>list_contact_fields</c> tells the model.</summary>
/// <param name="Fields">Every custom field a contact can carry, which the model fills from the chat.</param>
public sealed record ListContactFieldsAnswer(IReadOnlyList<ChatwootContactField> Fields);
