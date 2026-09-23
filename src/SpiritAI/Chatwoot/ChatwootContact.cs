namespace SpiritAI.Chatwoot;

/// <summary>A contact as Chatwoot made it in the Spirit inbox.</summary>
/// <param name="Id">The contact's id in the Chatwoot account.</param>
/// <param name="SourceId">The key Chatwoot made for the contact in the inbox.</param>
public sealed record ChatwootContact(int Id, string SourceId);
