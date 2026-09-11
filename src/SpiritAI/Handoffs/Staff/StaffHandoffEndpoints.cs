using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.Threads;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// The inbox, as REST: the queue, the claim, the reply, and the close. Section 9.1 of the spec.
/// </summary>
public static class StaffHandoffEndpoints
{
    /// <summary>The route prefix the inbox answers on.</summary>
    public const string Pattern = "/v1/handoff";

    /// <summary>How many rows a listing holds when the caller asks for no size.</summary>
    public const int DefaultPageSize = 30;

    private const string One = $"{Pattern}/{{callId}}";

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
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapPost($"{One}/done", FinishAsync)
            .Describe("finishHandoff")
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
    /// <param name="options">The staff list.</param>
    /// <param name="body">The route, given the caller's key and their entry in the list.</param>
    /// <returns>What the route answered, 401 when there is no caller, or 403 when they are not staff.</returns>
    private static async Task<IResult> ForStaffAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        Func<string, HandoffStaffMember, Task<IResult>> body)
    {
        if (CallerPrincipal.KeyOf(http.User) is not { } key)
        {
            return TypedResults.Unauthorized();
        }

        if (StaffGate.MemberOf(http.User, options.Value) is not { } member)
        {
            return Problem(StatusCodes.Status403Forbidden, "Not staff.", "This caller is not on the staff list.");
        }

        return await body(key, member).ConfigureAwait(false);
    }

    /// <summary>The rows in one state, the queue by default.</summary>
    private static Task<IResult> ListAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        IHandoffStore store,
        ICallStore calls,
        string? status,
        int? limit,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, options, async (_, _) =>
        {
            if (!HandoffSummary.TryReadStatus(status ?? HandoffSummary.StatusOf(HandoffStatus.Waiting), out var read))
            {
                return Problem(
                    StatusCodes.Status400BadRequest,
                    "The request cannot be read.",
                    $"'{status}' is not a status. Send 'waiting', 'human', 'done', or nothing at all.");
            }

            var rows = await store
                .ListAsync(read, Math.Clamp(limit ?? DefaultPageSize, 1, HandoffStore.MaxListSize), cancellationToken)
                .ConfigureAwait(false);

            var items = await HandoffSummaries.OfAsync(store, calls, rows, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(new HandoffPage(items));
        });

    /// <summary>The chat's open handoff, or the one closed most recently.</summary>
    private static Task<IResult> FetchAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        IHandoffStore store,
        ICallStore calls,
        string callId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, options, async (_, _) =>
        {
            if (await store.LatestAsync(callId, cancellationToken).ConfigureAwait(false) is not { } row)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(await HandoffSummaries.OfAsync(store, calls, row, cancellationToken).ConfigureAwait(false));
        });

    /// <summary>The whole chat, in the shape the browser draws a thread from.</summary>
    private static Task<IResult> HistoryAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        IHandoffStore store,
        ICallStore calls,
        string callId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, options, async (_, _) =>
        {
            // A chat that never asked for a person is not staff's to read.
            if (await store.LatestAsync(callId, cancellationToken).ConfigureAwait(false) is null
                || await calls.GetAsync(callId, cancellationToken).ConfigureAwait(false) is not { } record)
            {
                return TypedResults.NotFound();
            }

            var rows = await calls.ReadAsync(callId, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(ThreadHistory.Of(record, rows));
        });

    /// <summary>Takes a waiting chat for the caller.</summary>
    private static Task<IResult> ClaimAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        IHandoffStore store,
        ICallStore calls,
        IHandoffTranscript transcript,
        IHandoffNotifier notifier,
        HandoffDesk desk,
        TimeProvider clock,
        ILoggerFactory loggers,
        string callId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, options, async (key, member) =>
        {
            var claim = await store.ClaimAsync(callId, key, member.Name, cancellationToken).ConfigureAwait(false);

            switch (claim.Result)
            {
                case HandoffClaimResult.AlreadyTaken:
                    return Problem(StatusCodes.Status409Conflict, "Somebody has this chat.", $"{claim.Row!.AssigneeName} took it.");

                case HandoffClaimResult.NotWaiting:
                    return TypedResults.NotFound();

                default:
                    break;
            }

            await NoteAsync(transcript, clock, loggers, callId, $"{member.Name} joined", cancellationToken).ConfigureAwait(false);

            await notifier.ClaimedAsync(callId, new HandoffAssignee(key, member.Name), cancellationToken).ConfigureAwait(false);

            // Everyone behind the chat just taken moved up one.
            await desk.AnnounceQueueAsync(cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(await HandoffSummaries.OfAsync(store, calls, claim.Row!, cancellationToken).ConfigureAwait(false));
        });

    /// <summary>Puts the caller's words in a chat they hold.</summary>
    private static Task<IResult> ReplyAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        IHandoffStore store,
        HandoffDesk desk,
        string callId,
        HandoffReplyRequest? body,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, options, async (key, member) =>
        {
            if (body is not { Text: { } text } || string.IsNullOrWhiteSpace(text))
            {
                return Problem(StatusCodes.Status400BadRequest, "The request cannot be read.", "text must be a non-blank string.");
            }

            if (await store.OpenAsync(callId, cancellationToken).ConfigureAwait(false) is not { } row)
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

            try
            {
                var created = await desk.StaffSaysAsync(row, member, text, cancellationToken).ConfigureAwait(false);

                return TypedResults.Created($"{Pattern}/{callId}/messages", created);
            }
            catch (NotSupportedException)
            {
                // The reply is the whole point of the request. With nowhere to put the words,
                // nothing happened, and the caller must hear that rather than a 201.
                return Problem(
                    StatusCodes.Status503ServiceUnavailable,
                    "Replies are not stored yet.",
                    "AgentCore cannot store a human reply yet.");
            }
        });

    /// <summary>Hands the chat back to the bot, from waiting or from human.</summary>
    private static Task<IResult> FinishAsync(
        HttpContext http,
        IOptions<HandoffOptions> options,
        IHandoffStore store,
        IHandoffTranscript transcript,
        IHandoffNotifier notifier,
        HandoffDesk desk,
        TimeProvider clock,
        ILoggerFactory loggers,
        string callId,
        CancellationToken cancellationToken)
        => ForStaffAsync(http, options, async (_, _) =>
        {
            if (await store.OpenAsync(callId, cancellationToken).ConfigureAwait(false) is not { } row)
            {
                return TypedResults.NotFound();
            }

            // The name to sign off with is read before the close: once the row is done, the open
            // read no longer finds it.
            var leaving = row.Status == HandoffStatus.Human ? row.AssigneeName : null;

            if (!await store.DoneAsync(callId, cancellationToken).ConfigureAwait(false))
            {
                return TypedResults.NotFound();
            }

            if (leaving is not null)
            {
                await NoteAsync(transcript, clock, loggers, callId, $"{leaving} left", cancellationToken).ConfigureAwait(false);
            }

            await notifier.DoneAsync(callId, cancellationToken).ConfigureAwait(false);

            // A chat closed straight from waiting leaves the line; the ones behind it move up.
            await desk.AnnounceQueueAsync(cancellationToken).ConfigureAwait(false);

            return TypedResults.NoContent();
        });

    /// <summary>Writes one of the host's own lines, "joined" or "left", into the chat.</summary>
    /// <remarks>
    /// These lines are decoration on a state change that has already committed. When AgentCore
    /// cannot take them yet, the claim or the close still happened, so the route must not report
    /// it as a failure: the miss is logged and the route answers as if the line had landed. A staff
    /// reply is not decoration and does not come through here; <see cref="ReplyAsync"/> lets the
    /// same refusal become a 503.
    /// </remarks>
    private static async Task NoteAsync(
        IHandoffTranscript transcript,
        TimeProvider clock,
        ILoggerFactory loggers,
        string callId,
        string text,
        CancellationToken cancellationToken)
    {
        var line = new ChatMessage(ChatRole.Assistant, text) { CreatedAt = clock.GetUtcNow() };
        SpeakerProperty.Attach(line, HandoffSpeaker.System());

        try
        {
            await transcript.AppendAsync(callId, line, cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException unavailable)
        {
            loggers.CreateLogger(typeof(StaffHandoffEndpoints))
                .LogWarning(unavailable, "The line '{Text}' was not written to call {CallId}.", text, callId);
        }
    }

    private static ProblemHttpResult Problem(int statusCode, string title, string detail)
        => TypedResults.Problem(detail, statusCode: statusCode, title: title);
}
