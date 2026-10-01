namespace SpiritAI.Chatwoot;

/// <summary>The contact that has a phone number.</summary>
/// <param name="Id">The contact's id in the account.</param>
/// <param name="SourceId">Its key in the Spirit inbox, or null when it has no place there yet.</param>
public sealed record ChatwootPhoneContact(int Id, string? SourceId);
