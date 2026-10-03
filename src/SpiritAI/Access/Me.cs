namespace SpiritAI.Access;

/// <summary>Who the caller is and what they may do, for the web app to draw only what is allowed.</summary>
/// <param name="Id">Their id in <c>neon_auth."user"</c>.</param>
/// <param name="Name">Their name, as Neon Auth has it.</param>
/// <param name="Email">Their email, as Neon Auth has it.</param>
/// <param name="Banned">Whether Spirit bans them. A banned caller holds nothing.</param>
/// <param name="Permissions">What they hold, in <see cref="Access.Permissions.All"/> order.</param>
/// <param name="Agent">The chat agent they get, or <see langword="null"/>.</param>
public sealed record Me(Guid Id, string Name, string Email, bool Banned, IReadOnlyList<Permission> Permissions, Permission? Agent);
