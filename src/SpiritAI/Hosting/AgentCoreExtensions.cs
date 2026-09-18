using System.ComponentModel;

using AgentCore.Application.Tools;
using AgentCore.AspNetCore.DependencyInjection;
using AgentCore.AspNetCore.Endpoints;
using AgentCore.Hosting;

using SpiritAI.Handoffs.Bot;
using SpiritAI.Knowledge;
using SpiritAI.Lookup;

namespace SpiritAI.Hosting;

/// <summary>
/// Everything this host says about AgentCore that <c>spirit.yaml</c> cannot say for itself.
/// </summary>
public static class AgentCoreExtensions
{
    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the serial reader.</summary>
    public const string SerialBinding = "ParseSerial";

    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the unit reader.</summary>
    public const string UnitBinding = "AskUnit";

    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the bot's door into the handoff queue.</summary>
    public const string RequestHumanBinding = "RequestHuman";

    /// <summary>The <c>entries:</c> key every route and store reads. Staff and visitors share it.</summary>
    public const string Entry = "main";

    /// <summary>
    /// The one path a route with <c>{entry}</c> answers on for <see cref="Entry"/>, so a door that
    /// matches by path reads the same route the endpoint is mapped on.
    /// </summary>
    /// <param name="pattern">A route that carries <c>{entry}</c>.</param>
    /// <returns>The pattern, with <see cref="Entry"/> filled in.</returns>
    public static string RouteOf(string pattern)
    {
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return pattern.Replace(
            "{" + ResponsesEndpointRouteBuilderExtensions.EntryRouteParameter + "}", Entry, StringComparison.Ordinal);
    }

    /// <summary>Registers AgentCore, carrying this host's analyzers and bindings.</summary>
    /// <param name="builder">The host being built.</param>
    /// <returns>The same builder, so a host chains its calls.</returns>
    public static WebApplicationBuilder AddSpiritAgentCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAgentCoreHost();

        builder.Services
            .AddOptions<AgentCoreOptions>()
            .Configure<IServiceProvider, IHostEnvironment>(Configure);

        return builder;
    }

    /// <summary>Writes this host's word over the defaults AgentCore filled in.</summary>
    /// <param name="options">The options the host is filling.</param>
    /// <param name="services">
    /// The container. A binding reads its lookup out of this when the model calls the tool, which
    /// is long after everything is built: asking for one here instead would close a circle, because
    /// <see cref="UnitDesk"/> reaches the tool registry, and the registry is what these options
    /// are being read to build. <see cref="RequestHumanTool"/> is scoped, since the desk under it
    /// holds the database context, so its binding opens a scope for the one call.
    /// </param>
    /// <param name="environment">Locates the skills folder relative to the host, not the working directory.</param>
    private static void Configure(AgentCoreOptions options, IServiceProvider services, IHostEnvironment environment)
        => options
            .UseSkills(Path.Combine(environment.ContentRootPath, "skills"))
            .UseKnowledgeQueryAnalyzers(new IdentifierCodeAnalyzer())
            .Bind(
                SerialBinding,
                ([Description("The number the person offered as a serial number, exactly as they wrote it.")] string serial)
                    => SerialNumber.Parse(serial))
            .Bind(
                UnitBinding,
                (
                    [Description("A 16 digit serial number, when the person gave one.")] string? serialNo,
                    [Description("A work order number, as in 845435-1.")] string? orderNumber,
                    [Description("The customer's name, email or phone, exactly as the person wrote it.")] string? customer,
                    CancellationToken cancellationToken)
                    => services.GetRequiredService<UnitDesk>()
                        .ReadAsync(serialNo, orderNumber, customer, cancellationToken))
            .Bind(
                RequestHumanBinding,
                async (
                    [Description("Why the person needs a human, in one sentence, in the person's own words.")] string reason,
                    [Description("The person's email address, exactly as they gave it, so a reply reaches them if they step away. Omit it only if they declined to give one.")] string? email,
                    ToolCallScope scope,
                    CancellationToken cancellationToken) =>
                {
                    await using var container = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

                    return await container.ServiceProvider.GetRequiredService<RequestHumanTool>()
                        .AskAsync(scope.CallId, reason, email, cancellationToken)
                        .ConfigureAwait(false);
                });
}
