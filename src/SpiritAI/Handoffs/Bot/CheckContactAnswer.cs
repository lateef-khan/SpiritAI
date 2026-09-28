namespace SpiritAI.Handoffs.Bot;

/// <summary>What <c>check_contact</c> tells the model.</summary>
/// <param name="Phone">The phone in E.164 form, or <see langword="null"/> when none was given or it is not valid.</param>
/// <param name="Email">The email, trimmed and lower-cased, or <see langword="null"/> when none was given or it is not valid.</param>
/// <param name="Invalid">The fields that were given but are not valid: <c>phone</c>, <c>email</c>, or both.</param>
public sealed record CheckContactAnswer(string? Phone, string? Email, IReadOnlyList<string> Invalid);
