using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;

using SpiritAI.Access;
using SpiritAI.Hub;
using SpiritAI.Twenty;

namespace SpiritAI.Settings;

/// <summary>Settings → People: every route needs <see cref="Permission.SettingsPeople"/>.</summary>
internal static class PeopleEndpoints
{
    /// <summary>
    /// Lets a ban with no body and no content type match its route.
    /// </summary>
    private static readonly AcceptsMetadata AnyBody = new([], typeof(BanBody), isOptional: true);

    public static RouteGroupBuilder MapPeople(this RouteGroupBuilder settings)
    {
        var people = settings.MapGroup("/people").RequirePermission(Permission.SettingsPeople);

        people.MapGet("", ListAsync).Describe("listPeople").Produces<IReadOnlyList<PersonRow>>();

        people.MapPost("", AddAsync).Describe("addPerson")
            .Produces<AddedPerson>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapPut("/{id:guid}/roles", SetRolesAsync).Describe("setPersonRoles")
            .Produces<PersonRow>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapPost("/{id:guid}/ban", BanAsync).Describe("banPerson")
            .WithMetadata(AnyBody)
            .Produces<BanAnswer>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapPost("/{id:guid}/unban", UnbanAsync).Describe("unbanPerson")
            .Produces<BanAnswer>()
            .Produces(StatusCodes.Status404NotFound);

        people.MapDelete("/{id:guid}", DeleteAsync).Describe("deletePerson")
            .Produces<PersonDeletion>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapDelete("/{id:guid}/{app}", UnlinkAsync).Describe("unlinkPerson")
            .Produces<PersonRow>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        people.MapPost("/{id:guid}/{app}", LinkAsync).Describe("linkPerson")
            .Produces<PersonRow>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return settings;
    }

    private static async Task<IResult> ListAsync(People people, CancellationToken cancellationToken)
        => TypedResults.Ok(await people.ListAsync(cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> AddAsync(AddPersonBody body, HttpContext http, PersonAdding adding, CancellationToken cancellationToken)
    {
        var name = body.Name?.Trim() ?? "";
        var email = body.Email?.Trim().ToLowerInvariant() ?? "";

        if (name.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
        {
            return TypedResults.Problem("Type a name and an email.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (body.RoleIds is null)
        {
            return NoRoleList();
        }

        var outcome = await adding.AddAsync(name, email, body.RoleIds, body.Desk, body.Crm, Caller.Of(http.User)!, cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            { Refused: { } refused } => AccessWriteResults.Refusal(refused),
            { EmailTaken: true } => TypedResults.Problem("Somebody already has that email.", statusCode: StatusCodes.Status409Conflict),
            { Banned: true } => PersonIsBanned(),
            _ => TypedResults.Ok(outcome.Added),
        };
    }

    private static async Task<IResult> SetRolesAsync(
        Guid id, RolesBody body, HttpContext http, AccessWriter writer, People people, CancellationToken cancellationToken)
        => body.RoleIds is null
            ? NoRoleList()
            : await RowOrRefusalAsync(
                await writer.SetRolesAsync(id, body.RoleIds, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false),
                id, people, cancellationToken).ConfigureAwait(false);

    /// <summary>Saves the ban, then takes the person out of Desk. A failed Desk step leaves the ban standing.</summary>
    private static async Task<IResult> BanAsync(
        Guid id, BanBody? body, HttpContext http, AccessWriter writer, BanDesk desk, People people, CancellationToken cancellationToken)
    {
        var result = await writer.BanAsync(id, body?.Reason, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false);
        if (result != AccessWrite.Done)
        {
            return AccessWriteResults.Refusal(result);
        }

        var (step, detail) = await desk.LeaveAsync(id, cancellationToken).ConfigureAwait(false);
        return await AnswerAsync(id, step, detail, people, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Clears the ban, then puts the person back in Desk. A failed Desk step leaves the ban cleared.</summary>
    private static async Task<IResult> UnbanAsync(
        Guid id, HttpContext http, AccessWriter writer, BanDesk desk, People people, CancellationToken cancellationToken)
    {
        var result = await writer.UnbanAsync(id, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false);
        if (result != AccessWrite.Done)
        {
            return AccessWriteResults.Refusal(result);
        }

        var (step, detail) = await desk.RejoinAsync(id, cancellationToken).ConfigureAwait(false);
        return await AnswerAsync(id, step, detail, people, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> AnswerAsync(Guid id, StepResult desk, string? detail, People people, CancellationToken cancellationToken)
        => await people.OneAsync(id, cancellationToken).ConfigureAwait(false) is { } row
            ? TypedResults.Ok(new BanAnswer(row, desk, detail))
            : TypedResults.NotFound();

    private static async Task<IResult> DeleteAsync(
        Guid id, HttpContext http, AccessWriter writer, PersonRemoval removal, CancellationToken cancellationToken)
    {
        var caller = Caller.Of(http.User)!;

        if (caller.Id == id)
        {
            return AccessWriteResults.Refusal(AccessWrite.NotYourOwn);
        }

        var cleared = await writer.SetRolesAsync(id, [], caller, cancellationToken).ConfigureAwait(false);

        return cleared == AccessWrite.Done
            ? TypedResults.Ok(await removal.DeleteAsync(id, cancellationToken).ConfigureAwait(false))
            : AccessWriteResults.Refusal(cleared);
    }

    private static async Task<IResult> LinkAsync(
        Guid id, string app, People people, LinkPerson linker, CancellationToken cancellationToken)
    {
        if ((app != HubApps.Desk && app != HubApps.Crm) || await people.OneAsync(id, cancellationToken).ConfigureAwait(false) is not { } person)
        {
            return TypedResults.NotFound();
        }

        if (person.Banned)
        {
            return PersonIsBanned();
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

        return await people.OneAsync(id, cancellationToken).ConfigureAwait(false) is { } row ? TypedResults.Ok(row) : TypedResults.NotFound();
    }

    /// <summary>The person loses the app, so the caller must hold everything the person's roles give, as for a ban or a delete.</summary>
    private static async Task<IResult> UnlinkAsync(
        Guid id, string app, HttpContext http, AccessWriter writer, PersonRemoval removal, People people, CancellationToken cancellationToken)
    {
        var check = await writer.CheckPersonAsync(id, Caller.Of(http.User)!, cancellationToken).ConfigureAwait(false);
        if (check != AccessWrite.Done)
        {
            return AccessWriteResults.Refusal(check);
        }

        if (app != HubApps.Desk && app != HubApps.Crm)
        {
            return TypedResults.NotFound();
        }

        try
        {
            if (!await removal.UnlinkAsync(id, app, cancellationToken).ConfigureAwait(false))
            {
                return TypedResults.NotFound();
            }
        }
        catch (CrmRefusedException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (CrmUnavailableException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "CRM is unavailable.");
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return TypedResults.Problem("Desk did not answer.", statusCode: StatusCodes.Status503ServiceUnavailable, title: "Desk is unavailable.");
        }

        return TypedResults.Ok(await people.OneAsync(id, cancellationToken).ConfigureAwait(false));
    }

    private static ProblemHttpResult PersonIsBanned()
    {
        return TypedResults.Problem("This person is banned. Unban them first.", statusCode: StatusCodes.Status409Conflict);
    }

    /// <summary>A body without <c>roleIds</c>; an empty list is how to give no roles.</summary>
    private static ProblemHttpResult NoRoleList()
    {
        return TypedResults.Problem("Pick their roles, or send an empty list for none.", statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> RowOrRefusalAsync(AccessWrite result, Guid id, People people, CancellationToken cancellationToken)
        => result == AccessWrite.Done
            ? TypedResults.Ok(await people.OneAsync(id, cancellationToken).ConfigureAwait(false))
            : AccessWriteResults.Refusal(result);
}
