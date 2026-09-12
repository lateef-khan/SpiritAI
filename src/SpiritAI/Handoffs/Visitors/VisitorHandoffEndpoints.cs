using System.Net.Mail;

using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Http.HttpResults;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.PublicChat;
using SpiritAI.Threads;

namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// The visitor's side of a handoff, as REST: the ask, the state, the email, and the words said
/// while waiting. Section 9.2 of the spec. Every route reads the visitor's key from
/// <see cref="VisitorPrincipal.Header"/> and proves the chat is theirs before it does anything.
/// </summary>
public static class VisitorHandoffEndpoints
{
    /// <summary>The route prefix the visitor's handoff routes answer on.</summary>
    public const string Pattern = "/v1/public/handoff";

    private const string One = $"{Pattern}/{{callId}}";

    /// <summary>Maps the visitor's handoff routes on <see cref="Pattern"/>.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapVisitorHandoffs(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(Pattern, AskAsync)
            .Describe("askForHuman")
            .Produces<HandoffState>(StatusCodes.Status201Created)
            .Produces<HandoffState>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet(One, StateAsync)
            .Describe("getHandoffState")
            .Produces<HandoffState>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPatch($"{One}/email", EmailAsync)
            .Describe("leaveEmail")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost($"{One}/messages", SayAsync)
            .Describe("sendVisitorMessage")
            .Produces<HandoffMessage>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    /// <summary>
    /// Names one route in the OpenAPI document, and declares the refusal every route here can
    /// answer: a request with no usable visitor key.
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
            .WithTags("PublicHandoff")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    /// <summary>Runs a route body against a chat the visitor owns, or refuses the request.</summary>
    /// <param name="http">The request, carrying the visitor's key.</param>
    /// <param name="calls">The store the row is read from.</param>
    /// <param name="callId">The call the request named, which may be anything at all.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="body">The route.</param>
    /// <returns>
    /// What the route answered, 400 when there is no usable key, or 404 when the chat is not this
    /// visitor's. A wrong key and a missing chat look the same on purpose.
    /// </returns>
    private static async Task<IResult> ForOwnedAsync(
        HttpContext http,
        ICallStore calls,
        string callId,
        CancellationToken cancellationToken,
        Func<Task<IResult>> body)
    {
        var sent = http.Request.Headers[VisitorPrincipal.Header].ToString();

        if (!VisitorPrincipal.IsWellFormed(sent))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "No visitor key.",
                $"Send a well-formed {VisitorPrincipal.Header} header: letters, digits, '_' and '-', at most {VisitorPrincipal.MaxLength} characters.");
        }

        if (await ThreadOwnership.ReadAsync(calls, callId, VisitorPrincipal.KeyOf(sent), cancellationToken).ConfigureAwait(false)
            is null)
        {
            return TypedResults.NotFound();
        }

        return await body().ConfigureAwait(false);
    }

    /// <summary>Asks for a person on the visitor's chat.</summary>
    private static Task<IResult> AskAsync(
        HttpContext http,
        ICallStore calls,
        HandoffDesk desk,
        VisitorAskRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is not { CallId: { } callId } || string.IsNullOrWhiteSpace(callId))
        {
            return Task.FromResult<IResult>(
                Problem(StatusCodes.Status400BadRequest, "The request cannot be read.", "callId must be a non-blank string."));
        }

        return ForOwnedAsync(http, calls, callId, cancellationToken, async () =>
        {
            var asked = await desk.AskAsync(callId, HandoffAskedBy.Visitor, body.Reason, cancellationToken).ConfigureAwait(false);

            var state = await desk.StateAsync(callId, cancellationToken).ConfigureAwait(false);

            return asked.Created
                ? TypedResults.Created($"{Pattern}/{callId}", state)
                : TypedResults.Ok(state);
        });
    }

    /// <summary>Where the visitor's chat stands: the truth after a reconnect.</summary>
    private static Task<IResult> StateAsync(
        HttpContext http,
        ICallStore calls,
        HandoffDesk desk,
        string callId,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, callId, cancellationToken, async ()
            => TypedResults.Ok(await desk.StateAsync(callId, cancellationToken).ConfigureAwait(false)));

    /// <summary>Records where a reply goes when the visitor is not there to read it.</summary>
    private static Task<IResult> EmailAsync(
        HttpContext http,
        ICallStore calls,
        HandoffDesk desk,
        string callId,
        VisitorEmailRequest? body,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, callId, cancellationToken, async () =>
        {
            if (body is not { Email: { } email } || !MailAddress.TryCreate(email, out _))
            {
                return Problem(StatusCodes.Status400BadRequest, "The request cannot be read.", "email must be an email address.");
            }

            if (!await desk.SetEmailAsync(callId, email, cancellationToken).ConfigureAwait(false))
            {
                return Problem(StatusCodes.Status409Conflict, "Nothing is waiting.", "Ask for a person first.");
            }

            return TypedResults.NoContent();
        });

    /// <summary>Puts the visitor's words in a chat that is waiting or with a person.</summary>
    private static Task<IResult> SayAsync(
        HttpContext http,
        ICallStore calls,
        HandoffDesk desk,
        string callId,
        VisitorMessageRequest? body,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, callId, cancellationToken, async () =>
        {
            if (body is not { Text: { } text } || string.IsNullOrWhiteSpace(text))
            {
                return Problem(StatusCodes.Status400BadRequest, "The request cannot be read.", "text must be a non-blank string.");
            }

            if (await desk.VisitorSaysAsync(callId, text, cancellationToken).ConfigureAwait(false) is not { } created)
            {
                return Problem(StatusCodes.Status409Conflict, "The assistant has this chat.", "Send it there.");
            }

            return TypedResults.Created($"{Pattern}/{callId}/messages", created);
        });

    private static ProblemHttpResult Problem(int statusCode, string title, string detail)
        => TypedResults.Problem(detail, statusCode: statusCode, title: title);
}
