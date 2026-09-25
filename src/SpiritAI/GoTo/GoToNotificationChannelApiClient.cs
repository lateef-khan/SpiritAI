using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpiritAI.GoTo;

/// <summary>The GoTo notification-channel calls.</summary>
public sealed class GoToNotificationChannelApiClient(
    HttpClient http,
    IGoToRequestAuthorizer authorizer,
    IGoToAuthTokenProvider tokens)
    : IGoToNotificationChannelApiClient
{
    /// <summary>The page size GoTo allows and uses by default.</summary>
    public const int PageSize = 100;

    /// <summary>A stop for a page marker that never runs out: 50 pages is 5,000 channels.</summary>
    private const int MaxPages = 50;

    private const string Channels = "notification-channel/v1/channels";

    /// <inheritdoc />
    public async Task<GoToChannel> CreateWebhookChannelAsync(
        string nickname, Uri webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentNullException.ThrowIfNull(webhookUrl);

        var body = new JsonObject
        {
            ["channelType"] = "Webhook",
            ["webhookChannelData"] = new JsonObject
            {
                ["webhook"] = new JsonObject { ["url"] = webhookUrl.AbsoluteUri },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Channels}/{Uri.EscapeDataString(nickname)}")
        {
            Content = JsonContent.Create(body),
        };

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return Read(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GoToChannel>> ListChannelsAsync(CancellationToken cancellationToken = default)
    {
        var channels = new List<GoToChannel>();
        string? marker = null;

        for (var page = 0; page < MaxPages; page++)
        {
            var path = marker is null
                ? $"{Channels}?pageSize={PageSize}"
                : $"{Channels}?pageSize={PageSize}&pageMarker={Uri.EscapeDataString(marker)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

            if (answer.TryGetProperty("items", out var items))
            {
                channels.AddRange(items.EnumerateArray().Select(Read));
            }

            marker = answer.TryGetProperty("nextPageMarker", out var next) ? next.GetString() : null;

            if (string.IsNullOrEmpty(marker))
            {
                return channels;
            }
        }

        throw new InvalidOperationException($"GoTo listed more than {MaxPages} pages of channels.");
    }

    /// <inheritdoc />
    public async Task DeleteChannelAsync(string nickname, string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"{Channels}/{Uri.EscapeDataString(nickname)}/{Uri.EscapeDataString(channelId)}");

        try
        {
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Gone already: another server deleted it first, or GoTo's list was a few seconds behind.
        }
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => GoToApiCall.SendAsync(http, authorizer, tokens, request, cancellationToken);

    private static GoToChannel Read(JsonElement channel)
    {
        var url = channel.TryGetProperty("webhookChannelData", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("webhook", out var webhook)
            && webhook.TryGetProperty("url", out var address)
                ? address.GetString()
                : null;

        var lifetime = channel.TryGetProperty("channelLifetime", out var seconds) && seconds.ValueKind == JsonValueKind.Number
            ? seconds.GetInt64()
            : 0;

        return new GoToChannel(
            channel.GetProperty("channelId").GetString()!,
            channel.GetProperty("channelNickname").GetString()!,
            url,
            lifetime);
    }
}
