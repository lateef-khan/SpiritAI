namespace SpiritAI.Handoffs.Bot;

/// <summary>What <c>known_contact</c> tells the model: what the visitor's own contact already holds.</summary>
/// <param name="Phone">The phone number in E.164 form, or <see langword="null"/> when there is none.</param>
/// <param name="Email">The email, or <see langword="null"/> when there is none.</param>
public sealed record KnownContactAnswer(string? Phone, string? Email);
