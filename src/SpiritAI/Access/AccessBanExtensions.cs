using Microsoft.Extensions.Options;

using SpiritAI.Auth;

namespace SpiritAI.Access;

/// <summary>Refuses a banned person on every route Neon Auth guards.</summary>
public static class AccessBanExtensions
{
    /// <summary>Answers 401 on a guarded route, <see cref="MeEndpoints.Pattern"/> excepted, when the caller carries <see cref="Permissions.BannedClaimType"/>.</summary>
    public static IApplicationBuilder UseAccessBans(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices.GetRequiredService<IOptions<NeonAuthOptions>>().Value;

        return app.Use(async (context, next) =>
        {
            if (NeonAuthApplicationBuilderExtensions.IsGuarded(context.Request.Path, options)
                && !context.Request.Path.Equals(MeEndpoints.Pattern, StringComparison.OrdinalIgnoreCase)
                && context.User.HasClaim(Permissions.BannedClaimType, "true"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next().ConfigureAwait(false);
        });
    }
}
