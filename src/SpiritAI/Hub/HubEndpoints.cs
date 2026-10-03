using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Access;
using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.Twenty;

namespace SpiritAI.Hub;

/// <summary>
/// The Hub's home screen: which apps a caller's tiles show, and the one-time link that opens each.
/// </summary>
public static class HubEndpoints
{
    /// <summary>The route prefix the Hub answers on.</summary>
    public const string Pattern = "/v1/hub";

    /// <summary>Maps the Hub's routes on <see cref="Pattern"/>.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapHub(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet($"{Pattern}/apps", ListAsync)
            .Describe("listHubApps")
            .Produces<HubAppList>();

        endpoints.MapPost($"{Pattern}/{{app}}/sign-in", SignInAsync)
            .Describe("openHubApp")
            .Produces<HubSignIn>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>Names one route in the OpenAPI document, and declares the 401 every route here can answer.</summary>
    /// <param name="route">The route just mapped.</param>
    /// <param name="operationId">The name the generated client's function takes.</param>
    /// <returns>The same builder.</returns>
    private static RouteHandlerBuilder Describe(this RouteHandlerBuilder route, string operationId)
        => route
            .WithName(operationId)
            .WithTags("Hub")
            .Produces(StatusCodes.Status401Unauthorized);

    /// <summary>The tiles this caller earns: Chat and Settings from their permissions, Desk and CRM from a ready link alone.</summary>
    private static async Task<IResult> ListAsync(
        HttpContext http, IOptions<HubOptions> hub, SpiritDbContext db, CancellationToken cancellationToken)
    {
        if (SubjectOf(http.User) is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var held = Permissions.Of(http.User);

        var readyApps = await db.LinkedUsers
            .Where(link => link.UserId == userId && link.Ready)
            .Select(link => link.App)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<HubTile> tiles = [];

        if (Permissions.AgentOf(held) is not null)
        {
            tiles.Add(new HubTile("chat", "Spirit AI", "/chat/"));
        }

        if (readyApps.Contains(HubApps.Desk))
        {
            tiles.Add(new HubTile("desk", "Desk", $"{hub.Value.DeskUrl.TrimEnd('/')}/app"));
        }

        if (readyApps.Contains(HubApps.Crm))
        {
            tiles.Add(new HubTile("crm", "CRM", $"{hub.Value.CrmUrl.TrimEnd('/')}/"));
        }

        if (held.Contains(Permission.SettingsPeople) || held.Contains(Permission.SettingsRoles))
        {
            tiles.Add(new HubTile("settings", "Settings", "/chat/settings.html"));
        }

        return TypedResults.Ok(new HubAppList(tiles));
    }

    /// <summary>A one-time link into <paramref name="app"/>, for the caller's own ready link there.</summary>
    private static async Task<IResult> SignInAsync(
        HttpContext http,
        SpiritDbContext db,
        DeskUsers deskUsers,
        CrmUsers crmUsers,
        string app,
        CancellationToken cancellationToken)
    {
        if (SubjectOf(http.User) is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        if (app != HubApps.Desk && app != HubApps.Crm)
        {
            return TypedResults.NotFound();
        }

        var link = await db.LinkedUsers
            .SingleOrDefaultAsync(l => l.UserId == userId && l.App == app && l.Ready, cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            return TypedResults.NotFound();
        }

        var url = app == HubApps.Desk
            ? await deskUsers.SignInLinkAsync(int.Parse(link.ExternalId), cancellationToken).ConfigureAwait(false)
            : crmUsers.SignInUrl(link.ExternalId);

        return TypedResults.Ok(new HubSignIn(url));
    }

    /// <summary>Reads the caller's Neon Auth id, the key <see cref="LinkedUser.UserId"/> is filed under.</summary>
    /// <param name="user">The caller, as the Neon scheme authenticated them.</param>
    /// <returns>The id, or <see langword="null"/> when this caller has no stable identity.</returns>
    private static Guid? SubjectOf(ClaimsPrincipal? user)
        => Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
