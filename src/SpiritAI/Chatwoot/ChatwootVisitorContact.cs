namespace SpiritAI.Chatwoot;

/// <summary>The visitor's own contact, as the visitor's side of Chatwoot shows it.</summary>
/// <param name="Id">The contact's id in the Chatwoot account.</param>
/// <param name="Email">The email on the contact, if any.</param>
/// <param name="PhoneNumber">The phone number on the contact in E.164 form, if any.</param>
public sealed record ChatwootVisitorContact(int Id, string? Email, string? PhoneNumber);
