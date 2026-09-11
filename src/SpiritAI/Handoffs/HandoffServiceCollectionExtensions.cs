using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SpiritAI.Handoffs;

/// <summary>Registers the handoff store and the ports around it.</summary>
public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="HandoffStore"/> over the <c>spirit</c> schema, and the placeholder
    /// <see cref="IHandoffTranscript"/> that stands in until AgentCore ships its side.
    /// Add it after <c>AddSpiritDatabase</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHandoffs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<HandoffStore>();

        services.AddSingleton<IHandoffTranscript, UnavailableHandoffTranscript>();

        return services;
    }
}
