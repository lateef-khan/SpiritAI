namespace SpiritAI.Chatwoot;

/// <summary>One member of staff in the Chatwoot account.</summary>
/// <param name="Id">The user id a mention names.</param>
/// <param name="Name">What staff see.</param>
/// <param name="Email">The sign-in email.</param>
public sealed record ChatwootAgent(int Id, string Name, string Email);
