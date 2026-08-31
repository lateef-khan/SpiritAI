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
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where the section is read from.</param>
    /// <param name="configure">
    /// Applied after the configuration is bound, for what the code owns rather than a settings
    /// file: <see cref="NeonAuthOptions.OpenPathPrefixes"/> is set from where the open route is
    /// mapped.
    /// </param>
    public static IServiceCollection AddNeonAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<NeonAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        ArgumentNullException.ThrowIfNull(configuration);

        var options = services.AddOptions<NeonAuthOptions>()
            .Bind(configuration.GetSection(NeonAuthOptions.SectionName));

        if (configure is not null)
        {
            options.Configure(configure);
        }

        options
            .Validate(o => o.IsUsable(out _), FailureMessage(configuration))
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

        var options = app.ApplicationServices.GetRequiredService<IOptions<NeonAuthOptions>>().Value;

        app.UseAuthentication();

        app.Use(async (context, next) =>
        {
            // Open wins over protected, so one route may be carved out of a guarded prefix.
            if (Matches(context.Request.Path, options.OpenPathPrefixes)
                || !Matches(context.Request.Path, options.ProtectedPathPrefixes))
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

    private static bool Matches(PathString path, string[] prefixes)
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
