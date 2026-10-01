namespace SpiritAI.Chatwoot;

/// <summary>What Chatwoot answered when asked to create a conversation.</summary>
/// <param name="Id">The conversation's display id.</param>
/// <param name="Fresh">
/// Whether the conversation is new. With "Reopen same conversation" on, the answer can be the
/// contact's existing conversation, which holds messages.
/// </param>
public sealed record ChatwootCreatedConversation(int Id, bool Fresh);
