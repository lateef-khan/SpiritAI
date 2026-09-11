using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Handoffs;

/// <summary>Registers the handoff store and the ports around it.</summary>
public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IHandoffStore"/> over the <c>spirit</c> schema, the staff list bound from
    /// <see cref="HandoffOptions.SectionName"/>, and the placeholders that stand in until the rest
    /// of the feature lands: an <see cref="IHandoffTranscript"/> that refuses until AgentCore ships
    /// its side, and an <see cref="IHandoffNotifier"/> that pushes to nobody until there is a hub.
    /// Add it after <c>AddSpiritDatabase</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where the staff list is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHandoffs(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HandoffOptions>().Bind(configuration.GetSection(HandoffOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IHandoffStore, HandoffStore>();

        services.AddSingleton<IHandoffTranscript, UnavailableHandoffTranscript>();

        services.TryAddSingleton<IHandoffNotifier, SilentHandoffNotifier>();

        return services;
    }
}
