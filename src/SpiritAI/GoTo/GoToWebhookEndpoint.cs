using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>Maps the route GoTo posts call events to.</summary>
public static class GoToWebhookEndpoint
{
    /// <summary>The route. The secret in it is the only proof a post came from GoTo.</summary>
    public const string Pattern = "/goto/webhook/{secret}";

    /// <summary>The largest event body taken in.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>
    /// Maps <c>OPTIONS</c> (GoTo's check before it makes a channel) and <c>POST</c> (a call event)
    /// on <see cref="Pattern"/>. Both answer <c>404</c> to a wrong secret, and to every secret when
    /// <see cref="GoToOptions.WebhookSecret"/> is not set.
    /// </summary>
    /// <param name="endpoints">The route builder to map on.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapGoToWebhook(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMethods(Pattern, [HttpMethods.Options], Check).ExcludeFromDescription();

        endpoints.MapPost(Pattern, ReceiveAsync).ExcludeFromDescription();

        return endpoints;
    }

    /// <summary><c>200</c> with an empty body, which GoTo needs before it makes the channel.</summary>
    private static IResult Check(string secret, IOptions<GoToOptions> options)
        => IsTheSecret(secret, options.Value.WebhookSecret) ? Results.Ok() : Results.NotFound();

    /// <summary>Queues the event and answers at once; the work happens off the request.</summary>
    private static async Task<IResult> ReceiveAsync(
        string secret,
        HttpRequest request,
        IOptions<GoToOptions> options,
        GoToCallEventQueue queue,
        CancellationToken cancellationToken)
    {
        if (!IsTheSecret(secret, options.Value.WebhookSecret))
        {
            return Results.NotFound();
        }

        if (await ReadCappedAsync(request, cancellationToken).ConfigureAwait(false) is not { } body)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            using var callEvent = JsonDocument.Parse(body);

            queue.Add(callEvent.RootElement.Clone());
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        return Results.Ok();
    }

    /// <summary>
    /// Compares hashes, so the check takes the same time whatever the given secret's length or
    /// its first wrong character.
    /// </summary>
    private static bool IsTheSecret(string given, string expected)
        => expected.Length > 0
            && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(given)),
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    /// <summary>The body, or null once it passes <see cref="MaxBodyBytes"/>.</summary>
    private static async Task<byte[]?> ReadCappedAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return null;
        }

        using var body = new MemoryStream();
        var chunk = new byte[8192];
        int read;

        while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (body.Length + read > MaxBodyBytes)
            {
                return null;
            }

            body.Write(chunk, 0, read);
        }

        return body.ToArray();
    }
}
