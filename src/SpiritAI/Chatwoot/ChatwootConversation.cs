namespace SpiritAI.Chatwoot;

/// <summary>One Chatwoot conversation, as the bot reads it.</summary>
/// <param name="Id">The display id, which every conversation route is addressed by.</param>
/// <param name="Uuid">The id that stays unique when the Chatwoot database is reset.</param>
/// <param name="Status"><c>pending</c> while the AI has it; <c>open</c>, <c>resolved</c>, or <c>snoozed</c> otherwise.</param>
/// <param name="ContactId">The contact the conversation is with.</param>
public sealed record ChatwootConversation(int Id, Guid Uuid, string Status, int ContactId)
{
    /// <summary>Whether the AI is in charge: the bot holds the conversation.</summary>
    public bool Pending => Status == "pending";
}
