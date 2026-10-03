using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using SpiritAI.Database;

namespace SpiritAI.Access;

/// <summary><c>GET /v1/me</c>, the one <c>/v1</c> route a banned caller still reaches.</summary>
public static class MeEndpoints
{
    public const string Pattern = "/v1/me";

    /// <summary>Maps <see cref="Pattern"/>. Any signed-in caller may ask.</summary>
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Pattern, GetAsync)
            .WithName("getMe")
            .WithTags("Access")
            .Produces<Me>()
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(HttpContext http, SpiritDbContext db, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return TypedResults.Unauthorized();
        }

        var person = await db.Database
            .SqlQuery<NameAndEmail>($"""SELECT name AS "Name", email AS "Email" FROM neon_auth."user" WHERE id = {id}""")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (person is null)
        {
            return TypedResults.Unauthorized();
        }

        var held = Permissions.Of(http.User);
        IReadOnlyList<Permission> ordered = [.. Permissions.All.Select(info => info.Key).Where(held.Contains)];

        return TypedResults.Ok(new Me(
            id,
            person.Name,
            person.Email,
            http.User.HasClaim(Permissions.BannedClaimType, "true"),
            ordered,
            Permissions.AgentOf(ordered)));
    }

    private sealed record NameAndEmail(string Name, string Email);
}
