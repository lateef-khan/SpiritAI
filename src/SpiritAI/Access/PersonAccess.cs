namespace SpiritAI.Access;

/// <summary>What one person may do.</summary>
/// <param name="Banned">Whether <c>spirit.person_ban</c> has a row for them.</param>
/// <param name="Held">The permissions their roles give, kept through a ban so an unban loses nothing.</param>
/// <param name="Agent">The strongest chat agent in <paramref name="Held"/>, or <see langword="null"/>.</param>
public sealed record PersonAccess(bool Banned, IReadOnlyList<Permission> Held, Permission? Agent);
