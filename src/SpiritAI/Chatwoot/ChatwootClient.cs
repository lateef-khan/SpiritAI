using System.Globalization;
using System.Net;
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

    /// <summary>Reads one conversation as the bot.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Its ids, its status, and its contact.</returns>
    public async Task<ChatwootConversation> GetConversationAsync(int conversationId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, ConversationUrl(conversationId), body: null, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var shown = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return new ChatwootConversation(
            shown.GetProperty("id").GetInt32(),
            shown.GetProperty("uuid").GetGuid(),
            shown.GetProperty("status").GetString()!,
            shown.GetProperty("meta").GetProperty("sender").GetProperty("id").GetInt32());
    }

    /// <summary>Gives a conversation to a team, as the bot.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="teamId">The team, from <see cref="ListTeamsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task AssignTeamAsync(int conversationId, int teamId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Post, $"{ConversationUrl(conversationId)}/assignments", new JsonObject { ["team_id"] = teamId }, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Every team in the account, as the service user: the bot token cannot list them.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each team's id, name, and description.</returns>
    public async Task<IReadOnlyList<ChatwootTeam>> ListTeamsAsync(CancellationToken cancellationToken)
    {
        var teams = await GetAsServiceAsync($"{Account}/teams", cancellationToken).ConfigureAwait(false);

        return [.. teams.EnumerateArray().Select(t => new ChatwootTeam(
            t.GetProperty("id").GetInt32(),
            t.GetProperty("name").GetString() ?? string.Empty,
            t.GetProperty("description").GetString() ?? string.Empty))];
    }

    /// <summary>Every custom field a contact can carry, as the service user.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each field's key, name, type, and description.</returns>
    public async Task<IReadOnlyList<ChatwootContactField>> ListContactFieldsAsync(CancellationToken cancellationToken)
    {
        var fields = await GetAsServiceAsync($"{Account}/custom_attribute_definitions?attribute_model=1", cancellationToken)
            .ConfigureAwait(false);

        return [.. fields.EnumerateArray().Select(f => new ChatwootContactField(
            f.GetProperty("attribute_key").GetString()!,
            f.GetProperty("attribute_display_name").GetString() ?? string.Empty,
            f.GetProperty("attribute_display_type").GetString() ?? string.Empty,
            f.GetProperty("attribute_description").GetString() ?? string.Empty))];
    }

    /// <summary>
    /// Sets one of a contact's own fields, such as <c>phone_number</c> or <c>email</c>, through the
    /// staff API. It never merges contacts, so a value another contact has is refused.
    /// </summary>
    /// <param name="contactId">The contact's id in the account.</param>
    /// <param name="field">The field's name in Chatwoot's contact API.</param>
    /// <param name="value">The value to save.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Saved, or refused with Chatwoot's reason.</returns>
    public Task<ChatwootContactUpdate> UpdateContactFieldAsync(int contactId, string field, string value, CancellationToken cancellationToken)
        => UpdateContactAsync(contactId, new JsonObject { [field] = value }, cancellationToken);

    /// <summary>
    /// Sets custom fields on a contact through the staff API. Chatwoot keeps the contact's other
    /// custom fields.
    /// </summary>
    /// <param name="contactId">The contact's id in the account.</param>
    /// <param name="values">Each field's key, from <see cref="ListContactFieldsAsync"/>, and its value.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Saved, or refused with Chatwoot's reason.</returns>
    public Task<ChatwootContactUpdate> UpdateContactAttributesAsync(
        int contactId, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var attributes = new JsonObject();

        foreach (var (key, value) in values)
        {
            attributes[key] = value;
        }

        return UpdateContactAsync(contactId, new JsonObject { ["custom_attributes"] = attributes }, cancellationToken);
    }

    /// <summary>
    /// One page of a conversation's messages, as the visitor. Private notes are not on it.
    /// </summary>
    /// <param name="sourceId">The visitor's key.</param>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="before">Only messages older than this message id; <see langword="null"/> for the newest.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// Up to 20 messages, oldest first; <see langword="null"/> when the conversation is not this visitor's.
    /// </returns>
    public async Task<IReadOnlyList<ChatwootMessage>?> ListMessagesAsync(
        string sourceId, int conversationId, int? before, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"{ContactUrl(sourceId)}/conversations/{conversationId}/messages");

        if (before is { } id)
        {
            url += string.Create(CultureInfo.InvariantCulture, $"?before={id}");
        }

        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var messages = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return [.. messages.EnumerateArray().Select(ChatwootMessage.Read)];
    }

    /// <summary>Reads the visitor's own contact, as the visitor.</summary>
    /// <param name="sourceId">The visitor's key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The contact's id, email, and phone number.</returns>
    public async Task<ChatwootVisitorContact> GetVisitorContactAsync(string sourceId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(ContactUrl(sourceId), cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var contact = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return new ChatwootVisitorContact(
            contact.GetProperty("id").GetInt32(),
            contact.GetProperty("email").GetString(),
            contact.GetProperty("phone_number").GetString());
    }

    /// <summary>Reads the Spirit inbox's working hours through its public route.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The time zone and the hours of each day.</returns>
    public async Task<ChatwootInbox> GetInboxAsync(CancellationToken cancellationToken)
    {
        using var response = await http
            .GetAsync($"{Settings.BaseUrl.TrimEnd('/')}/public/api/v1/inboxes/{Settings.InboxIdentifier}", cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var inbox = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return new ChatwootInbox(
            inbox.GetProperty("timezone").GetString() ?? "UTC",
            inbox.GetProperty("working_hours_enabled").GetBoolean(),
            [.. inbox.GetProperty("working_hours").EnumerateArray().Select(ChatwootWorkingDay.Read)]);
    }

    private string ConversationUrl(int conversationId)
        => $"{Account}/conversations/{conversationId.ToString(CultureInfo.InvariantCulture)}";

    private string ContactUrl(string sourceId)
        => $"{Settings.BaseUrl.TrimEnd('/')}/public/api/v1/inboxes/{Settings.InboxIdentifier}/contacts/{Uri.EscapeDataString(sourceId)}";

    private async Task<JsonElement> GetAsServiceAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, body: null, cancellationToken, Settings.ServiceToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Chatwoot answers a refused change with <c>422</c> and <c>{"message": …}</c>.</summary>
    private async Task<ChatwootContactUpdate> UpdateContactAsync(int contactId, JsonObject body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
                HttpMethod.Patch,
                string.Create(CultureInfo.InvariantCulture, $"{Account}/contacts/{contactId}"),
                body,
                cancellationToken,
                Settings.ServiceToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var refused = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

            return new ChatwootContactUpdate(refused.GetProperty("message").GetString() ?? "refused");
        }

        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return ChatwootContactUpdate.Saved;
    }

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
