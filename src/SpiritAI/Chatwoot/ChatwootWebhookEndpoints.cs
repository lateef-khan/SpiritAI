using System.Text.Json;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The route the Spirit inbox's webhook posts to. It checks the signature, queues the event, and
/// answers at once; <see cref="ChatwootEventWorker"/> does the work.
/// </summary>
public static class ChatwootWebhookEndpoints
{
    /// <summary>The header Chatwoot names one delivery by. A retry of it carries the same value.</summary>
    public const string DeliveryHeader = "X-Chatwoot-Delivery";

    /// <summary>The largest body the route reads.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    private static readonly HybridCacheEntryOptions RememberDelivery = new() { Expiration = TimeSpan.FromMinutes(10) };

    /// <summary>Maps the webhook route, or nothing when no webhook secret is set.</summary>
    /// <param name="endpoints">The route builder to map on.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapChatwootWebhook(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settings = endpoints.ServiceProvider.GetRequiredService<IOptions<ChatwootOptions>>().Value;

        if (settings.WebhookEnabled)
        {
            endpoints.MapPost(settings.WebhookPattern, ReceiveAsync).ExcludeFromDescription();
        }

        return endpoints;
    }

    private static async Task<IResult> ReceiveAsync(
        HttpRequest request,
        IOptions<ChatwootOptions> options,
        ChatwootEventQueue queue,
        HybridCache deliveries,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (request.ContentLength > MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        if (buffer.Length > MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);

        if (!ChatwootSignature.IsValid(
                settings.WebhookSecret,
                request.Headers[ChatwootSignature.TimestampHeader],
                request.Headers[ChatwootSignature.SignatureHeader],
                body.Span,
                clock.GetUtcNow(),
                TimeSpan.FromSeconds(settings.MaxClockSkewSeconds)))
        {
            return Results.Unauthorized();
        }

        var delivery = request.Headers[DeliveryHeader].ToString();

        if (delivery.Length > 0)
        {
            // The factory runs only for a delivery the cache has not seen.
            var seen = true;

            await deliveries.GetOrCreateAsync(
                DeliveryKey(delivery),
                _ =>
                {
                    seen = false;
                    return ValueTask.FromResult(true);
                },
                RememberDelivery,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (seen)
            {
                return Results.Ok();
            }
        }

        ChatwootEvent? e;

        try
        {
            e = ChatwootEvent.Parse(JsonSerializer.Deserialize<JsonElement>(body.Span));
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (e is not null && ChatwootEventFilter.ActionOf(e) != ChatwootAction.Ignore)
        {
            queue.Enqueue(e);
        }

        return Results.Ok();
    }

    private static string DeliveryKey(string delivery) => "chatwoot-delivery:" + delivery;
}
