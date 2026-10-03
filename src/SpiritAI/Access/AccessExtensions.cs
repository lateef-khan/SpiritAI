using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SpiritAI.Access;

/// <summary>Registers the access model: the resolver, the claims it puts on a caller, one policy per permission, and the chat entry selector.</summary>
public static class AccessExtensions
{
    /// <param name="services">The host's services. The database and the cache are registered elsewhere.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddAccess(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAccessResolver, AccessResolver>();
        services.AddSingleton<IClaimsTransformation, AccessClaimsTransformation>();
        services.AddSingleton<AgentEntrySelector>();
        services.AddSingleton<AccessCache>();
        services.AddScoped<AccessWriter>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<AccessEntryCheck>();

        var authorization = services.AddAuthorizationBuilder();

        foreach (var info in Permissions.All)
        {
            var permission = info.Key;
            
            authorization.AddPolicy(
                Permissions.PolicyOf(permission),
                policy => policy.RequireAssertion(context => Permissions.Of(context.User).Contains(permission)));
        }

        return services;
    }
}
