using Microsoft.Extensions.DependencyInjection.Extensions;

using SpiritAI.Handoffs.Notifications;
using SpiritAI.RealTime;

namespace SpiritAI.Handoffs.RealTime;

/// <summary>Puts the handoff feature on the host's socket.</summary>
public static class HandoffRealTimeExtensions
{
    /// <summary>
    /// Registers <see cref="HandoffAdmission"/> with the hub and puts <see cref="HandoffNotifier"/>
    /// in place of the silent one <c>AddHandoffs</c> registered. Add it after <c>AddRealTime</c>
    /// and <c>AddHandoffs</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddHandoffRealTime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Scoped, not singleton: the gate under it reads the directory through the database
        // context, and the hub resolves its admissions inside the scope of each invocation.
        services.AddScoped<IRealTimeAdmission, HandoffAdmission>();

        services.Replace(ServiceDescriptor.Singleton<IHandoffNotifier, HandoffNotifier>());

        return services;
    }
}
