using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The Chatwoot calls Spirit makes.
/// </summary>
public sealed class ChatwootClient(HttpClient http, IOptions<ChatwootOptions> options)
{
    private const string TokenHeader = "api_access_token";

    private ChatwootOptions Settings => options.Value;

    private string Account => $"{Settings.BaseUrl.TrimEnd('/')}/api/v1/accounts/{Settings.AccountId}";

    /// <summary>
    /// Makes a contact in the Spirit inbox. Chatwoot makes its key: each call makes a new contact,
    /// so the caller keeps the answer and calls once per person.
    /// </summary>
    /// <param name="name">What staff see.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The contact's id and its key in the inbox.</returns>
    public async Task<ChatwootContact> CreateContactAsync(string name, CancellationToken cancellationToken)
    {
        var url = $"{Settings.BaseUrl.TrimEnd('/')}/public/api/v1/inboxes/{Settings.InboxIdentifier}/contacts";

        using var response = await http
            .PostAsJsonAsync(url, new JsonObject { ["name"] = name }, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var made = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return new ChatwootContact(made.GetProperty("id").GetInt32(), made.GetProperty("source_id").GetString()!);
    }

    /// <summary>Opens a conversation for a Spirit chat, waiting on the bot.</summary>
    /// <param name="sourceId">The contact's key, from <see cref="CreateContactAsync"/>.</param>
    /// <param name="spiritConversationId">The chat, kept on the conversation for the webhook to read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation's display id.</returns>
    public async Task<int> CreateConversationAsync(string sourceId, string spiritConversationId, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["source_id"] = sourceId,
            ["inbox_id"] = Settings.InboxId,
            ["status"] = "pending",
            ["custom_attributes"] = new JsonObject { [ChatwootEvent.SpiritConversationAttribute] = spiritConversationId },
        };

        using var response = await SendAsync(HttpMethod.Post, $"{Account}/conversations", body, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return created.GetProperty("id").GetInt32();
    }

    /// <summary>Posts one message into a conversation.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="content">The words.</param>
    /// <param name="fromVisitor">
    /// <see langword="true"/> for the visitor's words, which the API inbox files as the contact's;
    /// <see langword="false"/> for the AI's, which go out as the bot's.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task PostMessageAsync(int conversationId, string content, bool fromVisitor, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["content"] = content, ["message_type"] = fromVisitor ? "incoming" : "outgoing" };

        using var response = await SendAsync(HttpMethod.Post, $"{ConversationUrl(conversationId)}/messages", body, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Posts a private note, which only staff see.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="content">The note.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task PostNoteAsync(int conversationId, string content, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["content"] = content, ["message_type"] = "outgoing", ["private"] = true };

        using var response = await SendAsync(HttpMethod.Post, $"{ConversationUrl(conversationId)}/messages", body, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Hands a conversation to staff: status <c>open</c>. From the bot on a pending conversation,
    /// Chatwoot runs its own bot handoff, which alerts staff (<c>Conversation#bot_handoff!</c>).
    /// </summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task HandToStaffAsync(int conversationId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Post, $"{ConversationUrl(conversationId)}/toggle_status", new JsonObject { ["status"] = "open" }, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells staff that the visitor is typing, or stopped. It goes through the inbox's public API
    /// as the contact: the bot token cannot type as the contact. Chatwoot's dashboard drops
    /// "typing" by itself after 30 seconds.
    /// </summary>
    /// <param name="sourceId">The contact's key, from <see cref="CreateContactAsync"/>.</param>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="on"><see langword="true"/> for typing, <see langword="false"/> for stopped.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task ToggleTypingAsync(string sourceId, int conversationId, bool on, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{Settings.BaseUrl.TrimEnd('/')}/public/api/v1/inboxes/{Settings.InboxIdentifier}/contacts/{Uri.EscapeDataString(sourceId)}/conversations/{conversationId}/toggle_typing");

        using var response = await http
            .PostAsJsonAsync(url, new JsonObject { ["typing_status"] = on ? "on" : "off" }, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Every agent in the account, with the status Chatwoot shows for them.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each agent's id and status: <c>online</c>, <c>busy</c>, or <c>offline</c>.</returns>
    public async Task<IReadOnlyList<ChatwootAgent>> ListAgentsAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"{Account}/agents", body: null, cancellationToken, Settings.ServiceToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var agents = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return [.. agents.EnumerateArray().Select(a => new ChatwootAgent(
            a.GetProperty("id").GetInt32(),
            a.GetProperty("availability_status").GetString() ?? string.Empty))];
    }

    private string ConversationUrl(int conversationId)
        => $"{Account}/conversations/{conversationId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Sends as the bot, unless another <paramref name="token"/> is given.</summary>
    private async Task<HttpResponseMessage> SendAsync(
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

    /// <summary>Throws with Chatwoot's own words, which name what it refused.</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
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
