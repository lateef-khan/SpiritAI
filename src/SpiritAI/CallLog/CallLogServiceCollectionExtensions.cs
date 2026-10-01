using SpiritAI.GoTo;

namespace SpiritAI.CallLog;

/// <summary>Registers the copy of GoTo calls into Chatwoot.</summary>
public static class CallLogServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="CallLogOptions"/>, and, when <see cref="CallLogOptions.Enabled"/>, the
    /// queue, its worker, the start-up catch-up, and the handler of GoTo's report events.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where <see cref="CallLogOptions.SectionName"/> is read from.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddCallLog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(CallLogOptions.SectionName);
        services.Configure<CallLogOptions>(section);

        if (!section.GetValue<bool>(nameof(CallLogOptions.Enabled)))
        {
            return services;
        }

        services.AddSingleton<CallLogQueue>();
        services.AddScoped<CallLogCopier>();
        services.AddScoped<IGoToCallReportHandler, CallLogReportHandler>();
        services.AddHostedService<CallLogWorker>();
        services.AddHostedService<CallLogCatchUp>();

        return services;
    }
}
