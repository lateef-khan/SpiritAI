using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The wire under every Chatwoot class.
/// </summary>
public sealed class ChatwootApi(HttpClient http, IOptions<ChatwootOptions> options)
{
    /// <summary>How many messages Chatwoot puts on one page.</summary>
    public const int MessagePageSize = 20;

    private const string TokenHeader = "api_access_token";

    /// <summary>The settings, read on each use.</summary>
    public ChatwootOptions Settings => options.Value;

    /// <summary>The account's staff API root.</summary>
    public string Account => $"{Settings.BaseUrl.TrimEnd('/')}/api/v1/accounts/{Settings.AccountId}";

    /// <summary>One conversation's staff API route.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <returns>The route.</returns>
    public string ConversationUrl(int conversationId)
        => $"{Account}/conversations/{conversationId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Sends as the bot, unless another <paramref name="token"/> is given.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The route.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="token">The token to send in place of the bot's.</param>
    /// <returns>Chatwoot's answer, not yet checked.</returns>
    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, JsonObject? body, CancellationToken cancellationToken, string? token = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TokenHeader, token ?? Settings.BotToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends, checks the answer, and reads its JSON.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The route.</param>
    /// <param name="body">The JSON body, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="token">The token to send in place of the bot's.</param>
    /// <returns>The answer's JSON.</returns>
    public async Task<JsonElement> ReadAsync(
        HttpMethod method, string url, JsonObject? body, CancellationToken cancellationToken, string? token = null)
    {
        using var response = await SendAsync(method, url, body, cancellationToken, token).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends and checks the answer, whose body is not needed.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="url">The route.</param>
    /// <param name="body">The JSON body.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task WriteAsync(HttpMethod method, string url, JsonObject body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, url, body, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads as the service user: the bot token cannot list or search.</summary>
    /// <param name="url">The route.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer's JSON.</returns>
    public Task<JsonElement> GetAsServiceAsync(string url, CancellationToken cancellationToken)
        => ReadAsync(HttpMethod.Get, url, body: null, cancellationToken, Settings.ServiceToken);

    /// <summary>Throws with Chatwoot's own words, which name what it refused.</summary>
    /// <param name="response">Chatwoot's answer.</param>
    /// <param name="cancellationToken">Cancels reading the answer.</param>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        throw new HttpRequestException(
            $"Chatwoot answered {(int)response.StatusCode} to {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: {said}",
            inner: null,
            response.StatusCode);
    }
}
