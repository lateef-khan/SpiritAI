using System.Text.Json;

using AgentCore.Application.Ports;
using AgentCore.Application.State;
using AgentCore.Application.Tools.Registry;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;

namespace SpiritAI.Lookup;

/// <summary>
/// The unit panel's two routes, read straight off the tools the agent uses.
/// </summary>
public static class LookupEndpointRouteBuilderExtensions
{
    /// <summary>Where one machine is read.</summary>
    public const string UnitPattern = "/v1/units/{serial}";

    /// <summary>Where one work order is read.</summary>
    public const string OrderPattern = "/v1/orders/{orderNumber}";

    /// <summary>Registers the lookup and the way it reaches DAB.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddUnitLookup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ToolInvoker>(provider =>
        {
            var registry = provider.GetRequiredService<ToolRegistry>();

            return async (toolId, arguments, cancellationToken) =>
            {
                // An MCP tool is an AIFunction, so the host can call it directly. No second DAB
                // client, no duplicated URL or timeout, and the allow list in spirit.yaml stays the
                // one place tool access is declared.
                if (registry.Resolve(toolId) is not AIFunction function)
                {
                    throw new InvalidOperationException($"the tool '{toolId}' is not callable.");
                }

                var answer = await function
                    .InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>(arguments)), cancellationToken)
                    .ConfigureAwait(false);

                return JsonSerializer.SerializeToElement(answer);
            };
        });

        services.AddSingleton(provider => new UnitLookup(provider.GetRequiredService<ToolInvoker>()));
        services.AddSingleton(provider => new CustomerLookup(provider.GetRequiredService<ToolInvoker>()));
        services.AddSingleton(provider => new UnitDesk(
            provider.GetRequiredService<UnitLookup>(),
            provider.GetRequiredService<CustomerLookup>()));

        services.AddSingleton(provider => new ModelIndex(
            provider.GetService<IKnowledgeRetrievalPort>()?.GetService<IKnowledgeFacetReadPort>(),
            provider.GetRequiredService<VocabularyCache>(),
            provider.GetRequiredService<ToolInvoker>()));

        return services;
    }

    /// <summary>Maps both lookup routes.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapLookup(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(UnitPattern, UnitAsync)
            .WithName("getUnit")
            .WithTags("Lookup");

        endpoints.MapGet(OrderPattern, OrderAsync)
            .WithName("getOrder")
            .WithTags("Lookup");

        return endpoints;
    }

    /// <summary>Everything the panel shows for one machine.</summary>
    /// <remarks>
    /// The signature carries the response types, which is what a minimal API should do. Nothing
    /// here runs behind a helper the way <c>Threads/</c> does, so there is no union to widen and
    /// no reason to declare the shapes separately from the code that returns them.
    /// </remarks>
    /// <param name="lookup">Reads the tools.</param>
    /// <param name="serial">Sixteen digits.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The unit, 400 for a serial that is not one, or 404 for a machine nobody sold.</returns>
    private static async Task<Results<Ok<UnitDocument>, NotFound, ProblemHttpResult>> UnitAsync(
        UnitLookup lookup,
        string serial,
        CancellationToken cancellationToken)
    {
        if (!UnitLookup.IsSerial(serial))
        {
            return Refuse("a serial number is sixteen digits.");
        }

        return await lookup.ReadUnitAsync(serial, cancellationToken).ConfigureAwait(false) is { } unit
            ? TypedResults.Ok(unit)
            : TypedResults.NotFound();
    }

    /// <summary>One work order and the part lines on it.</summary>
    /// <param name="lookup">Reads the tools.</param>
    /// <param name="orderNumber">The <c>845435-1</c> key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The order, 400 for a key that is not one, or 404 when no order carries it.</returns>
    private static async Task<Results<Ok<OrderDocument>, NotFound, ProblemHttpResult>> OrderAsync(
        UnitLookup lookup,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        if (!UnitLookup.IsOrderNumber(orderNumber))
        {
            return Refuse("a work order number is digits, a dash, then digits, as in 845435-1.");
        }

        return await lookup.ReadOrderAsync(orderNumber, cancellationToken).ConfigureAwait(false) is { } order
            ? TypedResults.Ok(order)
            : TypedResults.NotFound();
    }

    private static ProblemHttpResult Refuse(string detail)
        => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "The request cannot be read.");
}
