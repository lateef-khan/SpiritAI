using System.ComponentModel;

using AgentCore.Application.Tools;
using AgentCore.AspNetCore.DependencyInjection;
using AgentCore.AspNetCore.Endpoints;
using AgentCore.Hosting;

using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Handoffs.Bot;
using SpiritAI.Handoffs.Model;
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

    /// <summary>The <c>binds:</c> name <c>spirit.yaml</c> gives the bot's door into the handoff queue.</summary>
    public const string RequestHumanBinding = "RequestHuman";

    /// <summary>The <c>binds:</c> name of the handoff's check of the office hours.</summary>
    public const string BusinessHoursBinding = "BusinessHours";

    /// <summary>The <c>binds:</c> name of the handoff's read of the phone and email the visitor gave before.</summary>
    public const string KnownContactBinding = "KnownContact";

    /// <summary>The <c>binds:</c> name of the handoff's check of a phone and email.</summary>
    public const string CheckContactBinding = "CheckContact";

    /// <summary>The <c>binds:</c> name of the handoff's list of teams.</summary>
    public const string ListTeamsBinding = "ListTeams";

    /// <summary>The <c>binds:</c> name of the handoff's list of contact fields.</summary>
    public const string ListContactFieldsBinding = "ListContactFields";

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
    /// <returns>The same builder, so a host chains its conversations.</returns>
    public static WebApplicationBuilder AddSpiritAgentCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddAgentCoreHost();

        builder.Services
            .AddOptions<AgentCoreOptions>()
            .Configure<IServiceProvider, IHostEnvironment>(Configure);

        builder.Services.AddConversationSweep();

        return builder;
    }

    /// <summary>
    /// Where a conversation's scratch folder lives: the shell's working directory, the file tools' store,
    /// and what <c>file.publish</c> copies out of. Nothing here outlives the conversation, so the machine's
    /// temp folder is the default; <c>Spirit:WorkspaceRoot</c> overrides it.
    /// </summary>
    private static string WorkspaceRoot(IConfiguration configuration)
        => configuration["Spirit:WorkspaceRoot"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(Path.GetTempPath(), "spiritai", "workspaces");

    /// <summary>Writes this host's word over the defaults AgentCore filled in.</summary>
    /// <param name="options">The options the host is filling.</param>
    /// <param name="services">
    /// The container. A binding reads its tool out of this when the model calls it, which is long
    /// after everything is built. <see cref="RequestHumanTool"/> and the handoff tools are scoped,
    /// since what is under them holds the database context, so each binding opens a scope for the
    /// one call.
    /// </param>
    /// <param name="environment">Locates the skills folder relative to the host, not the working directory.</param>
    private static void Configure(AgentCoreOptions options, IServiceProvider services, IHostEnvironment environment)
    {
        options.Cache = services.GetRequiredService<HybridCache>();

        options
            .UseSkills(Path.Combine(environment.ContentRootPath, "skills"))
            .UseWorkspace(WorkspaceRoot(services.GetRequiredService<IConfiguration>()))
            .UseKnowledgeQueryAnalyzers(new IdentifierCodeAnalyzer())
            .Bind(
                SerialBinding,
                ([Description("The number the person offered as a serial number, exactly as they wrote it.")] string serial)
                    => SerialNumber.Parse(serial))
            .Bind(
                RequestHumanBinding,
                async (
                    [Description("Why the person needs a human, in one sentence, in the person's own words.")] string reason,
                    [Description("The person's phone number, exactly as they gave it, so a member of staff can call them back. Omit it only if they declined to give one.")] string? phone,
                    [Description("The machine, type and model, such as \"XT485 treadmill\", from what the chat already says. Empty if unknown.")] string? product,
                    [Description("The machine's serial number exactly as the person gave it, as text, leading zeros kept. Empty if unknown.")] string? serial,
                    [Description("What was already tried in this chat, in one short sentence. Empty if nothing.")] string? tried,
                    [Description("What the person wants from staff, in one short sentence, such as \"a technician visit\".")] string? wants,
                    ToolCallScope scope,
                    CancellationToken cancellationToken) =>
                {
                    await using var container = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

                    return await container.ServiceProvider.GetRequiredService<RequestHumanTool>()
                        .AskAsync(scope.ConversationId, reason, phone, new HandoffSummary(product, serial, tried, wants), cancellationToken)
                        .ConfigureAwait(false);
                })
            .Bind(
                BusinessHoursBinding,
                (CancellationToken cancellationToken)
                    => InScopeAsync<BusinessHoursTool, BusinessHoursAnswer>(services, tool => tool.ReadAsync(cancellationToken)))
            .Bind(
                KnownContactBinding,
                (ToolCallScope scope, CancellationToken cancellationToken)
                    => InScopeAsync<KnownContactTool, KnownContactAnswer>(services, tool => tool.ReadAsync(scope.ConversationId, cancellationToken)))
            .Bind(
                CheckContactBinding,
                ([Description("The phone number exactly as the person gave it. Empty if they gave none.")] string? phone,
                 [Description("The email exactly as the person gave it. Empty if they gave none.")] string? email)
                    => CheckContactTool.Check(phone, email))
            .Bind(
                ListTeamsBinding,
                (CancellationToken cancellationToken)
                    => InScopeAsync<ListTeamsTool, ListTeamsAnswer>(services, tool => tool.ListAsync(cancellationToken)))
            .Bind(
                ListContactFieldsBinding,
                (CancellationToken cancellationToken)
                    => InScopeAsync<ListContactFieldsTool, ListContactFieldsAnswer>(services, tool => tool.ListAsync(cancellationToken)));
    }

    /// <summary>Runs one call of a scoped tool in a scope of its own.</summary>
    private static async Task<TAnswer> InScopeAsync<TTool, TAnswer>(IServiceProvider services, Func<TTool, Task<TAnswer>> call)
        where TTool : notnull
    {
        await using var container = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

        return await call(container.ServiceProvider.GetRequiredService<TTool>()).ConfigureAwait(false);
    }
}
