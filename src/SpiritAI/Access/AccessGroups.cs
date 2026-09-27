using System.Collections.Frozen;
using System.Security.Claims;

namespace SpiritAI.Access;

/// <summary>
/// The access group latter.
/// </summary>
public static class AccessGroups
{
    /// <summary>The entry the public widget and signed-in guests share.</summary>
    public const string MainEntry = "main";

    /// <summary>The claim identity <see cref="AccessClaimsTransformation"/> adds; only its role claims count.</summary>
    public const string IdentityType = "spirit-access";

    private static readonly FrozenDictionary<AccessGroup, (int Rank, string Entry)> Ladder =
        new Dictionary<AccessGroup, (int Rank, string Entry)>
        {
            [AccessGroup.Guest] = (0, MainEntry),
            [AccessGroup.Dealer] = (1, "dealer"),
            [AccessGroup.TechService] = (2, "staff"),
            [AccessGroup.InsideSales] = (2, "staff"),
            [AccessGroup.InsideSalesSupervisor] = (2, "staff"),
            [AccessGroup.TechServiceManager] = (3, "manager"),
            [AccessGroup.InsideSalesManager] = (3, "manager"),
            [AccessGroup.Admin] = (4, "admin"),
        }.ToFrozenDictionary();

    /// <summary>Every entry some group runs.</summary>
    public static IReadOnlyCollection<string> Entries { get; } = [.. Ladder.Values.Select(step => step.Entry).Distinct()];

    /// <summary>The group's rank. A higher rank has everything a lower one has.</summary>
    public static int RankOf(AccessGroup group) => Ladder[group].Rank;

    /// <summary>The <c>spirit.yaml</c> entry the group runs.</summary>
    public static string EntryOf(AccessGroup group) => Ladder[group].Entry;

    /// <summary>The group with the highest rank, or <see langword="null"/> when there is none.</summary>
    public static AccessGroup? Highest(IEnumerable<AccessGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        AccessGroup? highest = null;

        foreach (var group in groups)
        {
            if (highest is not { } current || RankOf(group) > RankOf(current))
            {
                highest = group;
            }
        }

        return highest;
    }

    /// <summary>The groups <see cref="AccessClaimsTransformation"/> put on a signed-in caller.</summary>
    public static IReadOnlyList<AccessGroup> Of(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return [];
        }

        return
        [
            .. user.Identities
                .Where(identity => identity.AuthenticationType == IdentityType)
                .SelectMany(identity => identity.FindAll(ClaimTypes.Role))
                .Select(claim => Enum.TryParse<AccessGroup>(claim.Value, out var group) ? group : (AccessGroup?)null)
                .OfType<AccessGroup>()
                .Distinct(),
        ];
    }
}
