namespace SpiritAI.Settings;

/// <summary>The Settings page's routes.</summary>
public static class SettingsEndpoints
{
    /// <summary>The route prefix Settings answers on.</summary>
    public const string Pattern = "/v1/settings";

    /// <summary>Maps People's and Roles' routes; each section asks for its own permission.</summary>
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settings = endpoints.MapGroup(Pattern);
        settings.MapPeople();
        settings.MapRoles();

        return endpoints;
    }

    /// <summary>Names one route in the OpenAPI document, and declares the 401 and 403 every route here can answer.</summary>
    internal static RouteHandlerBuilder Describe(this RouteHandlerBuilder route, string operationId)
        => route
            .WithName(operationId)
            .WithTags("Settings")
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
