using System.Text;
using System.Text.Json;

using AgentCore.Application.Calls;
using AgentCore.Application.Ports;

namespace SpiritAI.Threads;

/// <summary>
/// The thread list, as REST over <see cref="ICallStore"/>.
/// </summary>
public static class ThreadEndpointRouteBuilderExtensions
{
    /// <summary>The route prefix the thread list answers on.</summary>
    public const string Pattern = "/v1/threads";

    /// <summary>How many threads a page holds when the caller asks for no size.</summary>
    public const int DefaultPageSize = 30;

    /// <summary>The largest page this host will build, whatever a caller asks for.</summary>
    public const int MaxPageSize = 100;

    /// <summary>What the owner's claim on a call is called in <c>call_principal</c>.</summary>
    public const string OwnerRole = "owner";

    private const string One = $"{Pattern}/{{remoteId}}";

    /// <summary>Maps the thread list on <see cref="Pattern"/>.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapThreads(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Pattern, ListAsync);
        endpoints.MapPost(Pattern, CreateAsync);
        endpoints.MapGet(One, FetchAsync);
        endpoints.MapGet($"{One}/messages", HistoryAsync);
        endpoints.MapPost($"{One}/title", TitleAsync);
        endpoints.MapPatch(One, AmendAsync);
        endpoints.MapDelete(One, DeleteAsync);

        return endpoints;
    }

    /// <summary>Runs a route body for the caller behind the request, or refuses it.</summary>
    /// <param name="http">The request, carrying whoever the token named.</param>
    /// <param name="body">The route, given the caller's key.</param>
    /// <returns>What the route answered, or 401 when there is no caller.</returns>
    private static async Task<IResult> ForCallerAsync(HttpContext http, Func<string, Task<IResult>> body)
        => CallerPrincipal.KeyOf(http.User) is { } key
            ? await body(key).ConfigureAwait(false)
            : TypedResults.Unauthorized();

    /// <summary>Runs a route body against a thread the caller owns, or refuses the request.</summary>
    /// <param name="http">The request, carrying whoever the token named.</param>
    /// <param name="calls">The store the row is read from.</param>
    /// <param name="remoteId">The call the path named, which may be anything at all.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <param name="body">The route, given the caller's key and the row.</param>
    /// <returns>What the route answered, 401 when there is no caller, or 404 otherwise.</returns>
    private static Task<IResult> ForOwnedAsync(
        HttpContext http,
        ICallStore calls,
        string remoteId,
        CancellationToken cancellationToken,
        Func<string, CallRecord, Task<IResult>> body)
        => ForCallerAsync(http, async key
            => await ThreadOwnership.ReadAsync(calls, remoteId, key, cancellationToken).ConfigureAwait(false)
                is { } record
                ? await body(key, record).ConfigureAwait(false)
                : TypedResults.NotFound());

    /// <summary>One page of the caller's own threads.</summary>
    private static Task<IResult> ListAsync(
        HttpContext http,
        ICallStore calls,
        string? after,
        int? limit,
        string? status,
        CancellationToken cancellationToken)
        => ForCallerAsync(http, async key =>
        {
            CallStatus? narrowed = null;

            if (status is not null)
            {
                if (!ThreadSummary.TryReadStatus(status, out var read))
                {
                    return Refuse($"'{status}' is not a status. Send 'regular', 'archived', or nothing at all.");
                }

                narrowed = read;
            }

            var page = await calls
                .ListAsync(key, after, Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize), narrowed, cancellationToken)
                .ConfigureAwait(false);

            return TypedResults.Ok(new ThreadPage([.. page.Calls.Select(ThreadSummary.Of)], page.NextCursor));
        });

    /// <summary>Makes a thread, and gives the caller the only claim on it.</summary>
    private static Task<IResult> CreateAsync(
        HttpContext http,
        ICallStore calls,
        CancellationToken cancellationToken)
        => ForCallerAsync(http, async key =>
        {
            var callId = Guid.NewGuid().ToString("N");

            await calls.CreateAsync(callId, cancellationToken).ConfigureAwait(false);
            await calls.SetCustomAsync(callId, ThreadEnvelope.Build(key, app: null), cancellationToken).ConfigureAwait(false);
            await calls.AttachPrincipalAsync(callId, key, OwnerRole, cancellationToken).ConfigureAwait(false);

            return TypedResults.Created($"{Pattern}/{callId}", new ThreadCreated(callId, ExternalId: null));
        });

    /// <summary>One thread, when it is the caller's.</summary>
    private static Task<IResult> FetchAsync(
        HttpContext http,
        ICallStore calls,
        string remoteId,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, remoteId, cancellationToken, (_, record)
            => Task.FromResult<IResult>(TypedResults.Ok(ThreadSummary.Of(record))));

    /// <summary>One thread's whole conversation, in the shape the browser restores it from.</summary>
    private static Task<IResult> HistoryAsync(
        HttpContext http,
        ICallStore calls,
        string remoteId,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, remoteId, cancellationToken, async (_, record) =>
        {
            var rows = await calls.ReadAsync(remoteId, cancellationToken).ConfigureAwait(false);

            return TypedResults.Ok(ThreadHistory.Of(record, rows));
        });

    /// <summary>Names one thread from the words the browser sent.</summary>
    /// <remarks>
    /// The words come up in the body rather than out of the call store, because a browser asks for
    /// a name the moment the first message appears and AgentCore writes a turn only once it has
    /// finished. Reading the store here would read an empty call and answer nothing. It is also the
    /// shape assistant-ui's own adapters use, which is what keeps the browser side a plain fetch.
    /// </remarks>
    private static Task<IResult> TitleAsync(
        HttpContext http,
        ICallStore calls,
        ICallTitler titler,
        string remoteId,
        JsonElement? body,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, remoteId, cancellationToken, (_, _) =>
        {
            if (WordsOf(body) is not { } words)
            {
                return Task.FromResult<IResult>(
                    Refuse("messages must be an array of objects, each with string content."));
            }

            return Task.FromResult<IResult>(TypedResults.Stream(
                async stream =>
                {
                    await foreach (var piece in titler
                        .GenerateFromAsync(remoteId, words, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(piece), cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                },
                "text/plain; charset=utf-8"));
        });

    /// <summary>Reads the words to name out of a title request.</summary>
    /// <param name="body">What the browser sent, which may be nothing and may be anything at all.</param>
    /// <returns>
    /// The words, empty when the browser sent none, or <see langword="null"/> when the body is not
    /// a conversation and the request has to be refused.
    /// </returns>
    private static string? WordsOf(JsonElement? body)
    {
        if (body is not { } sent)
        {
            return string.Empty;
        }

        if (sent.ValueKind != JsonValueKind.Object
            || !sent.TryGetProperty("messages", out var messages)
            || messages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        StringBuilder words = new();

        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            if (words.Length > 0)
            {
                words.Append('\n');
            }

            words.Append(content.GetString());
        }

        return words.ToString();
    }

    /// <summary>Renames, archives, or rewrites the consumer-owned fields of one thread.</summary>
    private static Task<IResult> AmendAsync(
        HttpContext http,
        ICallStore calls,
        string remoteId,
        JsonElement body,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, remoteId, cancellationToken, async (key, _) =>
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                return Refuse("the body must be a JSON object.");
            }

            string? title = null;
            CallStatus? status = null;
            (bool Given, JsonElement? Value) custom = (false, null);

            if (body.TryGetProperty("title", out var titleValue))
            {
                if (titleValue.ValueKind != JsonValueKind.String)
                {
                    return Refuse("title must be a string.");
                }

                title = titleValue.GetString();
            }

            if (body.TryGetProperty("status", out var statusValue))
            {
                if (statusValue.ValueKind != JsonValueKind.String
                    || !ThreadSummary.TryReadStatus(statusValue.GetString(), out var read))
                {
                    return Refuse("status must be 'regular' or 'archived'.");
                }

                status = read;
            }

            if (body.TryGetProperty("custom", out var customValue))
            {
                custom = customValue.ValueKind switch
                {
                    JsonValueKind.Object => (true, customValue),
                    JsonValueKind.Null => (true, null),
                    _ => (false, null),
                };

                if (!custom.Given)
                {
                    return Refuse("custom must be a JSON object, or null to clear it.");
                }
            }

            if (title is not null)
            {
                await calls.RenameAsync(remoteId, title, cancellationToken).ConfigureAwait(false);
            }

            if (status is { } moved)
            {
                await calls.SetStatusAsync(remoteId, moved, cancellationToken).ConfigureAwait(false);
            }

            if (custom.Given)
            {
                // The owner is rewritten with it, because this column holds both and the store replaces
                // the whole value. Reading it from the token rather than from the row is deliberate:
                // ownership was already proved above, and re-using the proved key means a row whose
                // owner field were ever lost cannot be silently handed to whoever writes next.
                await calls
                    .SetCustomAsync(remoteId, ThreadEnvelope.Build(key, custom.Value), cancellationToken)
                    .ConfigureAwait(false);
            }

            return TypedResults.NoContent();
        });

    /// <summary>Erases one thread and every word of it.</summary>
    private static Task<IResult> DeleteAsync(
        HttpContext http,
        ICallStore calls,
        string remoteId,
        CancellationToken cancellationToken)
        => ForOwnedAsync(http, calls, remoteId, cancellationToken, async (_, _) =>
        {
            await calls.DeleteAsync(remoteId, cancellationToken).ConfigureAwait(false);

            return TypedResults.NoContent();
        });

    private static IResult Refuse(string detail)
        => TypedResults.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "The request cannot be read.");
}
