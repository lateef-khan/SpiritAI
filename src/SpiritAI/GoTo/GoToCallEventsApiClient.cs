using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>The GoTo call-events subscription calls.</summary>
public sealed class GoToCallEventsApiClient(
    HttpClient http,
    IGoToRequestAuthorizer authorizer,
    IGoToAuthTokenProvider tokens,
    IOptions<GoToOptions> options)
    : IGoToCallEventsApiClient
{
    private const string Subscriptions = "call-events/v1/subscriptions";

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
                ["events"] = new JsonArray("STARTING", "ACTIVE", "ENDING"),
            }),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Subscriptions) { Content = JsonContent.Create(body) };
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A 207 carries one status per account key, and any of them can be a refusal.
        var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var answer = JsonDocument.Parse(said);

        if (answer.RootElement.GetProperty("accountKeys").EnumerateArray().Any(a => a.GetProperty("status").GetInt32() is < 200 or >= 300))
        {
            throw new HttpRequestException(
                $"GoTo refused the call-events subscription for an account key: {said}", inner: null, response.StatusCode);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ReadSubscribedAccountKeysAsync(
        string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        // Without channelId GoTo answers 400 "must be a WebSocket channel", which only means the id is missing.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Subscriptions}?channelId={Uri.EscapeDataString(channelId)}");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return answer.TryGetProperty("accountKeys", out var keys)
            ? [.. keys.EnumerateArray().Select(k => k.GetString()!)]
            : [];
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => GoToApiCall.SendAsync(http, authorizer, tokens, request, cancellationToken);
}
