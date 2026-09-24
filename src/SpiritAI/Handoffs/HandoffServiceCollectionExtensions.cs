using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.Handoffs.Bot;

namespace SpiritAI.Handoffs;

/// <summary>Registers the handoff's tools.</summary>
public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// Adds the bot's <see cref="RequestHumanTool"/> with its <see cref="CallbackOptions"/>, and the
    /// handoff's Chatwoot tools. Add it after <c>AddChatwoot</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHandoffs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<CallbackOptions>().BindConfiguration(CallbackOptions.SectionName);

        services.AddScoped<RequestHumanTool>();

        services.AddScoped<BusinessHoursTool>();

        services.AddScoped<KnownContactTool>();

        services.AddScoped<ListTeamsTool>();

        services.AddScoped<ListContactFieldsTool>();

        return services;
    }
}
