using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SpiritAI.Auth;

/// <summary>Registers the Neon bearer scheme and the pieces behind it.</summary>
public static class NeonAuthServiceCollectionExtensions
{
    /// <summary>
    /// Adds Neon token validation, bound from the <see cref="NeonAuthOptions.SectionName"/>
    /// section.
    /// </summary>
    public static IServiceCollection AddNeonAuth(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<NeonAuthOptions>()
            .Bind(configuration.GetSection(NeonAuthOptions.SectionName))
            .Validate(options => options.IsUsable(out _), FailureMessage(configuration))
            .ValidateOnStart();

        services.TryAddSingletonTimeProvider();

        services.AddHttpClient<NeonSigningKeys>();

        services.AddSingleton<NeonTokenValidator>();

        services.AddAuthentication(NeonAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, NeonAuthenticationHandler>(
                NeonAuthenticationDefaults.Scheme, configureOptions: null);

        services.AddAuthorization();

        return services;
    }

    /// <summary>Reads the problem once, at registration, so the start-up failure names it.</summary>
    private static string FailureMessage(IConfiguration configuration)
    {
        var options = new NeonAuthOptions();
        configuration.GetSection(NeonAuthOptions.SectionName).Bind(options);
        options.IsUsable(out var problem);

        return problem;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}

/// <summary>Puts the lock on the routes that need it.</summary>
public static class NeonAuthApplicationBuilderExtensions
{
    /// <summary>
    /// Refuses any request under <see cref="NeonAuthOptions.ProtectedPathPrefixes"/> that does not
    /// carry a valid Neon token.
    /// </summary>
    public static IApplicationBuilder UseNeonAuthOnApi(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var prefixes = app.ApplicationServices.GetRequiredService<IOptions<NeonAuthOptions>>().Value.ProtectedPathPrefixes;

        app.UseAuthentication();

        app.Use(async (context, next) =>
        {
            if (!IsProtected(context.Request.Path, prefixes))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var result = await context.AuthenticateAsync(NeonAuthenticationDefaults.Scheme).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                await context.ChallengeAsync(NeonAuthenticationDefaults.Scheme).ConfigureAwait(false);
                return;
            }

            context.User = result.Principal;
            await next().ConfigureAwait(false);
        });

        return app;
    }

    private static bool IsProtected(PathString path, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
