using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Http.HttpResults;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.Reads;
using SpiritAI.Handoffs.Store;
using SpiritAI.Threads;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// The inbox, as REST: the queue, the claim, the reply, the close, and the read mark. Section 9.1 of the spec.
/// </summary>
public static class StaffHandoffEndpoints
{
    /// <summary>The route prefix the inbox answers on.</summary>
    public const string Pattern = "/v1/handoff";

    /// <summary>How many rows a listing holds when the caller asks for no size.</summary>
    public const int DefaultPageSize = 30;

    private const string One = $"{Pattern}/{{conversationId}}";

    /// <summary>Maps the inbox on <see cref="Pattern"/>.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapStaffHandoffs(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Pattern, ListAsync)
            .Describe("listHandoffs")
            .Produces<HandoffPage>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapGet($"{Pattern}/counts", CountAsync)
            .Describe("countHandoffs")
            .Produces<HandoffCounts>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        endpoints.MapGet(One, FetchAsync)
            .Describe("getHandoff")
            .Produces<HandoffSummary>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet($"{One}/messages", HistoryAsync)
            .Describe("getHandoffMessages")
            .Produces<ThreadHistory>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost($"{One}/claim", ClaimAsync)
            .Describe("claimHandoff")
            .Produces<HandoffSummary>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost($"{One}/messages", ReplyAsync)
            .Describe("replyToHandoff")
            .Produces<HandoffMessage>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost($"{One}/done", FinishAsync)
            .Describe("finishHandoff")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost($"{One}/seen", SeenAsync)
            .Describe("markHandoffSeen")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    /// <summary>
    /// Names one route in the OpenAPI document, and declares the two refusals every route here
    /// can answer: no token, and a token that is not staff's.
    /// </summary>
    /// <param name="route">The route just mapped.</param>
    /// <param name="operationId">
    /// The name the generated client's function takes. Chosen here rather than derived, because a
    /// derived name changes whenever the path does and every call site breaks with it.
    /// </param>
    /// <returns>The same builder.</returns>
    private static RouteHandlerBuilder Describe(this RouteHandlerBuilder route, string operationId)
        => route
            .WithName(operationId)
            .WithTags("Handoff")
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    /// <summary>Runs a route body for the member of staff behind the request, or refuses it.</summary>
    /// <param name="http">The request, carrying whoever the token named.</param>
    /// <param name="staff">Who counts as staff.</param>
    /// <param name="body">The route, given the caller's key and their entry in the list.</param>
    /// <returns>What the route answered, 401 when there is no caller, or 403 when they are not staff.</returns>
    private static async Task<IResult> ForStaffAsync(
        HttpContext http,
        StaffGate staff,
        Func<string, HandoffStaffMember, Task<IResult>> body)
    {
        if (CallerPrincipal.KeyOf(http.User) is not { } key)
        {
            return TypedResults.Unauthorized();
        }

        if (await staff.MemberOfAsync(http.User, http.RequestAborted).ConfigureAwait(false) is not { } member)
        {
            return Problem(StatusCodes.Status403Forbidden, "Not staff.", "This caller has no Neon sign-in.");
        }

        return await body(key, member).ConfigureAwait(false);
    }

    /// <summary>One page of the rows in one view, the open ones by default.</summary>
    private static Task<IResult> ListAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        IConversations conversations,
        IConversationReadStore reads,
        string? view,
        string? owner,
        string? order,
        int? limit,
        string? cursor,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (key, _) =>
        {
            if (!HandoffListQuery.TryRead(view, owner, order, cursor, key, out var query, out var problem))
            {
                return Problem(StatusCodes.Status400BadRequest, "The request cannot be read.", problem);
            }

            var page = await store
                .ListAsync(query!.Filter, Math.Clamp(limit ?? DefaultPageSize, 1, HandoffStore.MaxListSize), query.After, cancellationToken)
                .ConfigureAwait(false);

            var items = await HandoffSummaries.OfAsync(store, conversations, reads, key, page.Rows, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new HandoffPage(items, page.Next?.Encode()));
        });

    /// <summary>How many rows one view holds: the caller's, nobody's, and all of them.</summary>
    private static Task<IResult> CountAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        string? view,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (key, _) =>
        {
            if (!HandoffListQuery.TryReadView(view, out var read))
            {
                return Problem(
                    StatusCodes.Status400BadRequest,
                    "The request cannot be read.",
                    $"'{view}' is not a view. Send 'open', 'waiting', 'done', or nothing at all.");
            }

            return TypedResults.Ok(await store.CountAsync(read, key, cancellationToken).ConfigureAwait(false));
        });

    /// <summary>The chat's open handoff, or the one closed most recently.</summary>
    private static Task<IResult> FetchAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        IConversations conversations,
        IConversationReadStore reads,
        string conversationId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (key, _) =>
        {
            if (await store.LatestAsync(conversationId, cancellationToken).ConfigureAwait(false) is not { } row)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(await HandoffSummaries.OfAsync(store, conversations, reads, key, row, cancellationToken).ConfigureAwait(false));
        });

    /// <summary>One window of the chat, newest first, in the shape the browser draws a thread from.</summary>
    private static Task<IResult> HistoryAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        IConversations conversations,
        string conversationId,
        [AsParameters] HistoryQuery query,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (_, _) =>
        {
            if (!HistoryWindow.TryRead(query, out var window))
            {
                return HistoryWindow.Refuse(query);
            }

            // A chat that never asked for a person is not staff's to read.
            if (await store.LatestAsync(conversationId, cancellationToken).ConfigureAwait(false) is null
                || await conversations.LoadWindowAsync(conversationId, window, cancellationToken).ConfigureAwait(false) is not { } stored)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(ThreadHistory.Of(stored));
        });

    /// <summary>Takes a waiting chat for the caller.</summary>
    private static Task<IResult> ClaimAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        IConversations conversations,
        IConversationReadStore reads,
        IHandoffNotifier notifier,
        HandoffDesk desk,
        string conversationId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (key, member) =>
        {
            var claim = await store.ClaimAsync(conversationId, key, member.Name, cancellationToken).ConfigureAwait(false);

            switch (claim.Result)
            {
                case HandoffClaimResult.AlreadyTaken:
                    return Problem(StatusCodes.Status409Conflict, "Somebody has this chat.", $"{claim.Row!.AssigneeName} took it.");

                case HandoffClaimResult.NotWaiting:
                    return TypedResults.NotFound();

                default:
                    break;
            }

            await desk.NoteAsync(conversationId, $"{member.Name} joined", cancellationToken).ConfigureAwait(false);

            await notifier.ClaimedAsync(conversationId, new HandoffAssignee(key, member.Name), cancellationToken).ConfigureAwait(false);

            // Everyone behind the chat just taken moved up one.
            await desk.AnnounceQueueAsync(cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(await HandoffSummaries.OfAsync(store, conversations, reads, key, claim.Row!, cancellationToken).ConfigureAwait(false));
        });

    /// <summary>Puts the caller's words in a chat they hold.</summary>
    private static Task<IResult> ReplyAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        HandoffDesk desk,
        string conversationId,
        HandoffReplyRequest? body,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (key, member) =>
        {
            if (body is not { Text: { } text } || string.IsNullOrWhiteSpace(text))
            {
                return Problem(StatusCodes.Status400BadRequest, "The request cannot be read.", "text must be a non-blank string.");
            }

            if (await store.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false) is not { } row)
            {
                return TypedResults.NotFound();
            }

            if (row.Status != HandoffStatus.Human)
            {
                return Problem(StatusCodes.Status409Conflict, "Nobody has this chat.", "Take the chat first.");
            }

            if (!string.Equals(row.AssigneeKey, key, StringComparison.Ordinal))
            {
                return Problem(StatusCodes.Status403Forbidden, "Not yours.", $"{row.AssigneeName} has this chat.");
            }

            var created = await desk.StaffSaysAsync(row, member, text, cancellationToken).ConfigureAwait(false);

            return TypedResults.Created($"{Pattern}/{conversationId}/messages", created);
        });

    /// <summary>Hands the chat back to the bot, from waiting or from human.</summary>
    private static Task<IResult> FinishAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        IConversations conversations,
        IHandoffNotifier notifier,
        HandoffDesk desk,
        string conversationId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (_, _) =>
        {
            if (await store.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false) is not { } row)
            {
                return TypedResults.NotFound();
            }

            // The name to sign off with is read before the close: once the row is done, the open
            // read no longer finds it.
            var leaving = row.Status == HandoffStatus.Human ? row.AssigneeName : null;

            if (!await store.DoneAsync(conversationId, cancellationToken).ConfigureAwait(false))
            {
                return TypedResults.NotFound();
            }

            if (leaving is not null)
            {
                await desk.NoteAsync(conversationId, $"{leaving} left", cancellationToken).ConfigureAwait(false);
            }

            await notifier.DoneAsync(conversationId, cancellationToken).ConfigureAwait(false);

            // A chat closed straight from waiting leaves the line; the ones behind it move up.
            await desk.AnnounceQueueAsync(cancellationToken).ConfigureAwait(false);

            return TypedResults.NoContent();
        });

    /// <summary>
    /// Moves the caller's read mark to the chat's latest line, so the chat stops reading as
    /// unread for them. The browser calls it when it opens a chat and again as visitor lines land
    /// while the chat is on screen. Idempotent; a mark never moves back.
    /// </summary>
    private static Task<IResult> SeenAsync(
        HttpContext http,
        StaffGate staff,
        IHandoffStore store,
        IConversationReadStore reads,
        string conversationId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, staff, async (key, _) =>
        {
            // A chat that never asked for a person is not staff's to read.
            if (await store.LatestAsync(conversationId, cancellationToken).ConfigureAwait(false) is null)
            {
                return TypedResults.NotFound();
            }

            await reads.MarkSeenAsync(conversationId, key, cancellationToken).ConfigureAwait(false);

            return TypedResults.NoContent();
        });

    private static ProblemHttpResult Problem(int statusCode, string title, string detail)
        => TypedResults.Problem(detail, statusCode: statusCode, title: title);
}
