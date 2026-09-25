using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo notification-channel and call-events calls.
/// </summary>
public sealed class GoToCallEventsApiClient(
    HttpClient http,
    IGoToRequestAuthorizer authorizer,
    IGoToAuthTokenProvider tokens,
    IOptions<GoToOptions> options)
    : IGoToCallEventsApiClient
{
    /// <summary>The GoTo API host.</summary>
    public static readonly Uri ApiHost = new("https://api.goto.com/");

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

        using var response = await PostAsync(
                $"notification-channel/v1/channels/{Uri.EscapeDataString(nickname)}", body, cancellationToken)
            .ConfigureAwait(false);

        var made = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return new GoToChannel(
            made.GetProperty("channelId").GetString()!,
            made.GetProperty("channelNickname").GetString()!,
            made.GetProperty("channelLifetime").GetInt64());
    }

    /// <inheritdoc />
    public async Task SubscribeToCallEventsAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        var accountKey = options.Value.AccountKey;

        if (string.IsNullOrWhiteSpace(accountKey))
        {
            throw new InvalidOperationException($"{GoToOptions.SectionName}:{nameof(GoToOptions.AccountKey)} is missing.");
        }

        var body = new JsonObject
        {
            ["channelId"] = channelId,
            ["accountKeys"] = new JsonArray(new JsonObject
            {
                ["id"] = accountKey,
                ["events"] = new JsonArray("STARTING", "ENDING"),
            }),
        };

        using var response = await PostAsync("call-events/v1/subscriptions", body, cancellationToken).ConfigureAwait(false);

        // A 207 carries one status per account key, and any of them can be a refusal.
        var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var answer = JsonDocument.Parse(said);

        if (answer.RootElement.GetProperty("accountKeys").EnumerateArray().Any(a => a.GetProperty("status").GetInt32() is < 200 or >= 300))
        {
            throw new HttpRequestException(
                $"GoTo refused the call-events subscription for an account key: {said}", inner: null, response.StatusCode);
        }
    }

    /// <summary>Sends one signed POST and throws with GoTo's own words when it fails.</summary>
    private async Task<HttpResponseMessage> PostAsync(string path, JsonObject body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };

        await authorizer.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);

        var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await tokens.InvalidateBearerTokenAsync(cancellationToken).ConfigureAwait(false);
            }

            var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            throw new HttpRequestException(
                $"GoTo answered {(int)response.StatusCode} to POST /{path}: {said}", inner: null, response.StatusCode);
        }
    }
}
