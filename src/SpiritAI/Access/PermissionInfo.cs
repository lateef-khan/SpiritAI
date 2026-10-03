namespace SpiritAI.Access;

/// <summary>One permission as the Roles page lists it.</summary>
/// <param name="Key">The permission.</param>
/// <param name="Group">The heading it is listed under.</param>
/// <param name="Label">What it lets a person do, in a few words.</param>
/// <param name="Agent">Whether it is a chat agent. Agents come first in <see cref="Permissions.All"/>, strongest first.</param>
public sealed record PermissionInfo(Permission Key, string Group, string Label, bool Agent);
