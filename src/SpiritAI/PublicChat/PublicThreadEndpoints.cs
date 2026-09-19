using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;

using SpiritAI.Threads;

namespace SpiritAI.PublicChat;

/// <summary>
/// The widget's own thread, as REST: made before the first turn, found again after a reload.
/// Section 4.4 of the handoff spec. Nobody here is signed in; the visitor's key in
/// <see cref="VisitorPrincipal.Header"/> is the whole identity.
/// </summary>
public static class PublicThreadEndpoints
{
    /// <summary>The route prefix the widget's threads answer on.</summary>
    public const string Pattern = "/v1/public/threads";

    /// <summary>What the visitor's claim on a conversation is called in <c>conversation_principal</c>.</summary>
    public const string VisitorRole = "visitor";

    private const string One = $"{Pattern}/{{conversationId}}";

    /// <summary>Maps the widget's thread routes on <see cref="Pattern"/>.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapPublicThreads(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(Pattern, CreateAsync)
            .Describe("createPublicThread")
            .Produces<ThreadCreated>(StatusCodes.Status201Created);

        endpoints.MapGet($"{One}/messages", HistoryAsync)
            .Describe("getPublicThreadMessages")
            .Produces<ThreadHistory>()
            .Produces(StatusCodes.Status404NotFound);

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
            .WithTags("PublicThreads")
            .ProducesProblem(StatusCodes.Status400BadRequest);

    /// <summary>Runs a route body for the visitor behind the request, or refuses it.</summary>
    /// <param name="http">The request, carrying the visitor's key.</param>
    /// <param name="body">The route, given the key the visitor's conversations are filed under.</param>
    /// <returns>What the route answered, or 400 when there is no usable key.</returns>
    private static Task<IResult> ForVisitorAsync(HttpContext http, Func<string, Task<IResult>> body)
    {
        var sent = http.Request.Headers[VisitorPrincipal.Header].ToString();

        if (!VisitorPrincipal.IsWellFormed(sent))
        {
            return Task.FromResult<IResult>(TypedResults.Problem(
                $"Send a well-formed {VisitorPrincipal.Header} header: letters, digits, '_' and '-', at most {VisitorPrincipal.MaxLength} characters.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "No visitor key."));
        }

        return body(VisitorPrincipal.KeyOf(sent));
    }

    /// <summary>Makes a thread, and gives the visitor the only claim on it.</summary>
    private static Task<IResult> CreateAsync(
        HttpContext http,
        IConversations conversations,
        CancellationToken cancellationToken)
        => ForVisitorAsync(http, async key =>
        {
            var conversationId = Guid.NewGuid().ToString("N");

            await conversations.CreateAsync(conversationId, cancellationToken).ConfigureAwait(false);
            await conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(key, app: null), cancellationToken).ConfigureAwait(false);
            await conversations.AttachPrincipalAsync(conversationId, key, VisitorRole, cancellationToken).ConfigureAwait(false);

            return TypedResults.Created($"{Pattern}/{conversationId}/messages", new ThreadCreated(conversationId, ExternalId: null));
        });

    /// <summary>One thread's whole conversation, in the shape a reloaded widget restores it from.</summary>
    private static Task<IResult> HistoryAsync(
        HttpContext http,
        IConversations conversations,
        string conversationId,
        CancellationToken cancellationToken)
        => ForVisitorAsync(http, async key =>
        {
            if (await conversations.LoadAsync(conversationId, cancellationToken).ConfigureAwait(false) is not { } stored
                || !ThreadOwnership.Owns(stored.Conversation, key))
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(ThreadHistory.Of(stored));
        });
}
