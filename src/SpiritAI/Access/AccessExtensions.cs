using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

namespace SpiritAI.Access;

/// <summary>Registers roles, access groups, and the entry each group runs.</summary>
public static class AccessExtensions
{
    /// <summary>
    /// Adds the group lookup, the claims it puts on a signed-in caller, the <see cref="AccessPolicies"/>,
    /// and the entry selector the Responses route runs.
    /// </summary>
    /// <param name="services">The host's services. The database and the cache are registered elsewhere.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddAccess(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IUserAccess, DatabaseUserAccess>();
        services.AddSingleton<IClaimsTransformation, AccessClaimsTransformation>();
        services.AddSingleton<GroupEntrySelector>();
        services.AddHostedService<AccessEntryCheck>();

        services.AddAuthorizationBuilder()
            .AddPolicy(AccessPolicies.Staff, policy => policy.RequireAssertion(context => RankAtLeast(context.User, 2)))
            .AddPolicy(AccessPolicies.Manager, policy => policy.RequireAssertion(context => RankAtLeast(context.User, 3)))
            .AddPolicy(AccessPolicies.Admin, policy => policy.RequireAssertion(context => RankAtLeast(context.User, 4)));

        return services;
    }

    private static bool RankAtLeast(ClaimsPrincipal user, int rank)
        => AccessGroups.Highest(AccessGroups.Of(user)) is { } group && AccessGroups.RankOf(group) >= rank;
}
