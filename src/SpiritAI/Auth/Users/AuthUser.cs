namespace SpiritAI.Auth.Users;

/// <summary>One person with a Neon sign-in.</summary>
/// <param name="Id">Neon's id for them: what the token carries in <c>sub</c>.</param>
/// <param name="Name">The name they signed up with, as typed.</param>
/// <param name="Email">The address on their sign-in, as stored.</param>
public sealed record AuthUser(string Id, string Name, string Email);
