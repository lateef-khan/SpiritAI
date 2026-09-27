using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.GoTo;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Handoffs.Callback;

namespace SpiritAI.Handoffs;

/// <summary>Registers the handoff's tools.</summary>
public static class HandoffServiceCollectionExtensions
{
    /// <summary>
    /// Hand off dependency injection.
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

        services.AddScoped<CallStaff>();

        services.AddScoped<IGoToCallHandler, CallRingAlert>();

        services.AddScoped<IGoToCallHandler, CallbackCalled>();

        return services;
    }
}
