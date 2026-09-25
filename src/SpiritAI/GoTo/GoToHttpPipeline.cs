using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;

using Polly;

namespace SpiritAI.GoTo;

/// <summary>
/// Registers the GoTo token provider, the request signer, and the channel and call-events clients.
/// </summary>
public static class GoToHttpPipeline
{
    /// <summary>The GoTo API host.</summary>
    public static readonly Uri ApiHost = new("https://api.goto.com/");

    /// <summary>
    /// Adds the <see cref="GoToOptions"/> and the GoTo HTTP clients. Only safe methods are retried:
    /// a POST or DELETE is sent once.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="GoToOptions.SectionName"/> is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddGoToHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<GoToOptions>(configuration.GetSection(GoToOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient<IGoToAuthTokenProvider, GoToAuthTokenProvider>(
                client => client.BaseAddress = GoToAuthTokenProvider.TokenHost)
            .AddResilienceHandler("goto-auth", RetrySafeMethods);

        services.AddTransient<IGoToRequestAuthorizer, GoToRequestAuthorizer>();

        services.AddHttpClient<IGoToNotificationChannelApiClient, GoToNotificationChannelApiClient>(
                client => client.BaseAddress = ApiHost)
            .AddResilienceHandler("goto-channels", RetrySafeMethods);

        services.AddHttpClient<IGoToCallEventsApiClient, GoToCallEventsApiClient>(
                client => client.BaseAddress = ApiHost)
            .AddResilienceHandler("goto-call-events", RetrySafeMethods);

        return services;
    }

    private static void RetrySafeMethods(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        var retry = new HttpRetryStrategyOptions();
        retry.DisableForUnsafeHttpMethods();

        builder.AddRetry(retry);
    }
}
