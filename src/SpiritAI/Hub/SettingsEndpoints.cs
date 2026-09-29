using Microsoft.EntityFrameworkCore;

using SpiritAI.Access;
using SpiritAI.Database;
using SpiritAI.Twenty;

namespace SpiritAI.Hub;

/// <summary>
/// The Settings page.
/// </summary>
public static class SettingsEndpoints
{
    /// <summary>The route prefix Settings answers on.</summary>
    public const string Pattern = "/v1/settings";

    /// <summary>Maps Settings' routes on <see cref="Pattern"/>, behind <see cref="AccessPolicies.Admin"/>.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapSettings(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settings = endpoints.MapGroup(Pattern).RequireAuthorization(AccessPolicies.Admin);

        settings.MapGet("/people", ListAsync)
            .Describe("listPeople")
            .Produces<IReadOnlyList<PersonRow>>()
            .Produces(StatusCodes.Status403Forbidden);

        settings.MapPost("/people/{id:guid}/{app}", LinkAsync)
            .Describe("linkPerson")
            .Produces<PersonRow>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
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
            .WithTags("Settings")
            .Produces(StatusCodes.Status401Unauthorized);

    /// <summary>Every Person, with their groups and link states.</summary>
    private static async Task<IResult> ListAsync(SpiritDbContext db, CancellationToken cancellationToken)
        => TypedResults.Ok(await PeopleAsync(db, cancellationToken).ConfigureAwait(false));

    /// <summary>Creates <paramref name="app"/>'s user for one Person, then answers with their row.</summary>
    private static async Task<IResult> LinkAsync(
        Guid id, string app, SpiritDbContext db, LinkPerson linker, CancellationToken cancellationToken)
    {
        if (app != HubApps.Desk && app != HubApps.Crm)
        {
            return TypedResults.NotFound();
        }

        try
        {
            await linker.RunAsync(id, app, cancellationToken).ConfigureAwait(false);
        }
        catch (CrmUnavailableException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "CRM is unavailable.");
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }

        var people = await PeopleAsync(db, cancellationToken).ConfigureAwait(false);

        return people.SingleOrDefault(person => person.Id == id) is { } row
            ? TypedResults.Ok(row)
            : TypedResults.NotFound();
    }

    /// <summary>
    /// Reads every Person straight off <c>neon_auth."user"</c> (the context maps only its id
    /// column, <see cref="NeonUserStub"/>), then their groups and links in one query each.
    /// </summary>
    private static async Task<IReadOnlyList<PersonRow>> PeopleAsync(SpiritDbContext db, CancellationToken cancellationToken)
    {
        var people = await db.Database
            .SqlQuery<PersonRecord>($"""SELECT id, name, email FROM neon_auth."user" ORDER BY name""")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grants = await db.UserRoles
            .Join(db.Roles, grant => grant.Role, role => role.Name, (grant, role) => new { grant.UserId, role.AccessGroup })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var links = await db.LinkedUsers.ToListAsync(cancellationToken).ConfigureAwait(false);

        var groupsByPerson = grants.ToLookup(grant => grant.UserId, grant => grant.AccessGroup);
        var linksByPerson = links.ToLookup(link => link.UserId);

        return
        [
            .. people.Select(person => new PersonRow(
                person.Id,
                person.Name,
                person.Email,
                GroupsOf(groupsByPerson[person.Id]),
                StateOf(linksByPerson[person.Id], HubApps.Desk),
                StateOf(linksByPerson[person.Id], HubApps.Crm))),
        ];
    }

    /// <summary>Reads the access groups a role's <c>access_group</c> names, skipping a role that grants none.</summary>
    private static IReadOnlyList<AccessGroup> GroupsOf(IEnumerable<string?> accessGroupNames)
        => [.. accessGroupNames
            .Select(name => name is not null && Enum.TryParse<AccessGroup>(name, out var group) ? group : (AccessGroup?)null)
            .OfType<AccessGroup>()
            .Distinct()];

    /// <summary>Whether a Person's link to one app is missing, unfinished, or ready.</summary>
    private static LinkState StateOf(IEnumerable<LinkedUser> links, string app)
        => links.SingleOrDefault(link => link.App == app) switch
        {
            null => LinkState.None,
            { Ready: true } => LinkState.Ready,
            _ => LinkState.Unfinished,
        };

    /// <summary>The three columns of <c>neon_auth."user"</c> the People list reads.</summary>
    private sealed record PersonRecord(Guid Id, string Name, string Email);
}
