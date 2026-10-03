using SpiritAI.Access;

namespace SpiritAI.Settings;

/// <summary>Settings → Roles. Reading needs either Settings permission; writing needs <see cref="Permission.SettingsRoles"/>.</summary>
internal static class RoleEndpoints
{
    public static RouteGroupBuilder MapRoles(this RouteGroupBuilder settings)
    {
        settings.MapGet("/permissions", () => TypedResults.Ok(Permissions.All))
            .RequireAnyPermission(Permission.SettingsPeople, Permission.SettingsRoles)
            .Describe("listPermissions")
            .Produces<IReadOnlyList<PermissionInfo>>();

        settings.MapGet("/roles", ListAsync)
            .RequireAnyPermission(Permission.SettingsPeople, Permission.SettingsRoles)
            .Describe("listRoles")
            .Produces<IReadOnlyList<RoleRow>>();

        var roles = settings.MapGroup("/roles").RequirePermission(Permission.SettingsRoles);

        roles.MapPost("", CreateAsync).Describe("createRole")
            .Produces<RoleRow>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        roles.MapPut("/{id:guid}", UpdateAsync).Describe("updateRole")
            .Produces<RoleRow>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        roles.MapDelete("/{id:guid}", DeleteAsync).Describe("deleteRole")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return settings;
    }

    private static async Task<IResult> ListAsync(Roles roles, CancellationToken cancellationToken)
        => TypedResults.Ok(await roles.ListAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> CreateAsync(RoleDraft draft, HttpContext http, AccessWriter writer, Roles roles, CancellationToken cancellationToken)
    {
        if (Incomplete(draft) is { } incomplete)
        {
            return incomplete;
        }

        var (result, id) = await writer.CreateRoleAsync(draft, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false);

        return result == AccessWrite.Done
            ? TypedResults.Ok(await roles.OneAsync(id, cancellationToken).ConfigureAwait(false))
            : AccessWriteResults.Refusal(result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, RoleDraft draft, HttpContext http, AccessWriter writer, Roles roles, CancellationToken cancellationToken)
    {
        if (Incomplete(draft) is { } incomplete)
        {
            return incomplete;
        }

        var result = await writer.UpdateRoleAsync(id, draft, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false);

        return result == AccessWrite.Done
            ? TypedResults.Ok(await roles.OneAsync(id, cancellationToken).ConfigureAwait(false))
            : AccessWriteResults.Refusal(result);
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext http, AccessWriter writer, CancellationToken cancellationToken)
    {
        var result = await writer.DeleteRoleAsync(id, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false);

        return result == AccessWrite.Done ? TypedResults.NoContent() : AccessWriteResults.Refusal(result);
    }

    /// <summary>A body with a null name or no permission list; an empty list is how to grant nothing.</summary>
    private static IResult? Incomplete(RoleDraft draft) => draft switch
    {
        { Name: null } => AccessWriteResults.Refusal(AccessWrite.Invalid),
        { Permissions: null } => TypedResults.Problem(
            "Pick the role's permissions, or send an empty list for none.", statusCode: StatusCodes.Status400BadRequest),
        _ => null,
    };
}
