using System.Collections.Frozen;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace SpiritAI.Access;

/// <summary>The only list of permissions, the claims they travel as, and the agent each chat permission runs.</summary>
public static class Permissions
{
    /// <summary>The claim identity <see cref="AccessClaimsTransformation"/> adds; only its claims count.</summary>
    public const string IdentityType = "spirit-access";

    /// <summary>One claim of this type per permission held, its value the key.</summary>
    public const string ClaimType = "spirit:permission";

    /// <summary>The claim a banned person gets instead of any permission.</summary>
    public const string BannedClaimType = "spirit:banned";

    /// <summary>The entry the public widget and signed-in guests share.</summary>
    public const string MainEntry = "main";

    private const string ChatAgent = "Chat agent";

    /// <summary>Every permission: the chat agents first, strongest first, then the rest.</summary>
    public static IReadOnlyList<PermissionInfo> All { get; } =
    [
        new(Permission.ChatAgentAdmin, ChatAgent, "Admin agent", Agent: true),
        new(Permission.ChatAgentManager, ChatAgent, "Manager agent", Agent: true),
        new(Permission.ChatAgentStaff, ChatAgent, "Staff agent", Agent: true),
        new(Permission.ChatAgentDealer, ChatAgent, "Dealer agent", Agent: true),
        new(Permission.ChatAgentGuest, ChatAgent, "Guest agent", Agent: true),
        new(Permission.LookupUnits, "Lookup", "Look up a unit by serial", Agent: false),
        new(Permission.LookupOrders, "Lookup", "Look up a work order", Agent: false),
        new(Permission.SettingsPeople, "Settings", "Manage people", Agent: false),
        new(Permission.SettingsRoles, "Settings", "Manage roles", Agent: false),
    ];

    private static readonly FrozenDictionary<Permission, string> AgentEntries = new Dictionary<Permission, string>
    {
        [Permission.ChatAgentAdmin] = "admin",
        [Permission.ChatAgentManager] = "manager",
        [Permission.ChatAgentStaff] = "staff",
        [Permission.ChatAgentDealer] = "dealer",
        [Permission.ChatAgentGuest] = MainEntry,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Permission, string> Keys = Enum.GetValues<Permission>().ToFrozenDictionary(
        permission => permission,
        permission => typeof(Permission).GetField(permission.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name);

    private static readonly FrozenDictionary<string, Permission> ByKey =
        Keys.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>Every entry a chat agent runs, which <c>spirit.yaml</c> must declare.</summary>
    public static IReadOnlyCollection<string> Entries { get; } = [.. AgentEntries.Values];

    /// <summary>The key a permission is stored and sent as.</summary>
    public static string KeyOf(Permission permission) => Keys[permission];

    /// <summary>Reads a stored key; <see langword="false"/> for a key the code no longer has.</summary>
    public static bool TryParse(string key, out Permission permission) => ByKey.TryGetValue(key, out permission);

    /// <summary>The authorization policy that asks for one permission.</summary>
    public static string PolicyOf(Permission permission) => $"permission:{KeyOf(permission)}";

    /// <summary>The strongest chat agent held, or <see langword="null"/> when none is.</summary>
    public static Permission? AgentOf(IEnumerable<Permission> held)
    {
        ArgumentNullException.ThrowIfNull(held);

        var set = held.ToHashSet();
        return All.FirstOrDefault(info => info.Agent && set.Contains(info.Key))?.Key;
    }

    /// <summary>The <c>spirit.yaml</c> entry a chat agent permission runs.</summary>
    public static string EntryOf(Permission agent) => AgentEntries[agent];

    /// <summary>The permissions <see cref="AccessClaimsTransformation"/> put on a signed-in caller.</summary>
    public static IReadOnlySet<Permission> Of(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return FrozenSet<Permission>.Empty;
        }

        return user.Identities
            .Where(identity => identity.AuthenticationType == IdentityType)
            .SelectMany(identity => identity.FindAll(ClaimType))
            .Select(claim => TryParse(claim.Value, out var permission) ? permission : (Permission?)null)
            .OfType<Permission>()
            .ToHashSet();
    }
}
