using System.Collections.Concurrent;

namespace SpiritAI.Access;

/// <summary>Turns stored permission keys into permissions, in <see cref="Permissions.All"/> order.</summary>
internal static class HeldPermissions
{
    private static readonly ConcurrentDictionary<string, bool> Reported = new(StringComparer.Ordinal);

    /// <summary>The permissions a person or a role holds.</summary>
    /// <param name="admin">Whether the built-in Admin role is among them, which holds every permission.</param>
    /// <param name="keys">The stored keys. A key the code no longer has is skipped and logged once per process.</param>
    /// <param name="log">Where an unknown key is reported.</param>
    public static IReadOnlyList<Permission> Read(bool admin, IEnumerable<string> keys, ILogger log)
    {
        if (admin)
        {
            return [.. Permissions.All.Select(info => info.Key)];
        }

        var held = new HashSet<Permission>();

        foreach (var key in keys)
        {
            if (Permissions.TryParse(key, out var permission))
            {
                held.Add(permission);
            }
            else if (Reported.TryAdd(key, true))
            {
                log.LogWarning(
                    "spirit.role_permission names {Permission}, which the code does not know. It is ignored, and the next save of its role drops it.",
                    key);
            }
        }

        return [.. Permissions.All.Select(info => info.Key).Where(held.Contains)];
    }
}
