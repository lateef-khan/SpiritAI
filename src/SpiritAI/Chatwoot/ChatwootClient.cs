using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The Chatwoot client.
/// </summary>
public sealed class ChatwootClient(HttpClient http, IOptions<ChatwootOptions> options)
{
    /// <summary>A stop for a conversation that never runs out: 500 pages is 10,000 messages.</summary>
    private const int MaxMessagePages = 500;

    private readonly ChatwootApi api = new(http, options);

    private ChatwootOptions Settings => api.Settings;

    private string Account => api.Account;

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

        using var response = await api.SendAsync(HttpMethod.Post, $"{ConversationUrl(conversationId)}/messages", body, cancellationToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Hands a conversation to staff: status <c>open</c>. From the bot on a pending conversation,
    /// Chatwoot runs its own bot handoff, which alerts staff (<c>Conversation#bot_handoff!</c>).
    /// </summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task HandToStaffAsync(int conversationId, CancellationToken cancellationToken)
    {
        using var response = await api.SendAsync(
                HttpMethod.Post, $"{ConversationUrl(conversationId)}/toggle_status", new JsonObject { ["status"] = "open" }, cancellationToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads one conversation as the bot.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Its ids, its status, and its contact.</returns>
    public async Task<ChatwootConversation> GetConversationAsync(int conversationId, CancellationToken cancellationToken)
    {
        using var response = await api.SendAsync(HttpMethod.Get, ConversationUrl(conversationId), body: null, cancellationToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

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
        using var response = await api.SendAsync(
                HttpMethod.Post, $"{ConversationUrl(conversationId)}/assignments", new JsonObject { ["team_id"] = teamId }, cancellationToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gives a conversation to one member of staff, as the bot.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="agentId">The agent's Chatwoot user id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task AssignAgentAsync(int conversationId, int agentId, CancellationToken cancellationToken)
    {
        using var response = await api.SendAsync(
                HttpMethod.Post, $"{ConversationUrl(conversationId)}/assignments", new JsonObject { ["assignee_id"] = agentId }, cancellationToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Every team in the account, as the service user: the bot token cannot list them.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each team's id, name, and description.</returns>
    public async Task<IReadOnlyList<ChatwootTeam>> ListTeamsAsync(CancellationToken cancellationToken)
    {
        var teams = await api.GetAsServiceAsync($"{Account}/teams", cancellationToken).ConfigureAwait(false);

        return [.. teams.EnumerateArray().Select(ReadTeam)];
    }

    /// <summary>Every custom field a contact can carry, as the service user.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each field's key, name, type, and description.</returns>
    public async Task<IReadOnlyList<ChatwootContactField>> ListContactFieldsAsync(CancellationToken cancellationToken)
    {
        var fields = await api.GetAsServiceAsync($"{Account}/custom_attribute_definitions?attribute_model=1", cancellationToken)
            .ConfigureAwait(false);

        return [.. fields.EnumerateArray().Select(f => new ChatwootContactField(
            f.GetProperty("attribute_key").GetString()!,
            f.GetProperty("attribute_display_name").GetString() ?? string.Empty,
            f.GetProperty("attribute_display_type").GetString() ?? string.Empty,
            f.GetProperty("attribute_description").GetString() ?? string.Empty))];
    }

    /// <summary>
    /// A contact's newest conversations, as the service user. A plain agent only sees conversations
    /// in its own inboxes, so the service user must be a member of the Spirit inbox.
    /// </summary>
    /// <param name="contactId">The contact's id in the account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Up to 25 conversations, newest first.</returns>
    public async Task<IReadOnlyList<ChatwootContactConversation>> ListContactConversationsAsync(int contactId, CancellationToken cancellationToken)
    {
        var listed = await api.GetAsServiceAsync(
                string.Create(CultureInfo.InvariantCulture, $"{Account}/contacts/{contactId}/conversations"), cancellationToken)
            .ConfigureAwait(false);

        return [.. listed.GetProperty("payload").EnumerateArray().Select(c => new ChatwootContactConversation(
            c.GetProperty("id").GetInt32(),
            c.GetProperty("status").GetString()!,
            DateTimeOffset.FromUnixTimeSeconds(c.GetProperty("last_activity_at").GetInt64()),
            c.GetProperty("meta").TryGetProperty("team", out var team) && team.ValueKind == JsonValueKind.Object
                ? ReadTeam(team)
                : null))];
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

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

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

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

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

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var inbox = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);

        return new ChatwootInbox(
            inbox.GetProperty("timezone").GetString() ?? "UTC",
            inbox.GetProperty("working_hours_enabled").GetBoolean(),
            [.. inbox.GetProperty("working_hours").EnumerateArray().Select(ChatwootWorkingDay.Read)]);
    }

    /// <summary>The contact whose phone number is exactly <paramref name="e164"/>. Chatwoot allows one.</summary>
    /// <param name="e164">The number in E.164.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The contact, or null when nobody has the number.</returns>
    public async Task<ChatwootPhoneContact?> FindPhoneContactAsync(string e164, CancellationToken cancellationToken)
    {
        var found = await api.GetAsServiceAsync($"{Account}/contacts/search?q={Uri.EscapeDataString(e164)}", cancellationToken)
            .ConfigureAwait(false);

        // Chatwoot's search also matches part of a number, so only the exact one counts.
        return found.GetProperty("payload").EnumerateArray()
            .Where(c => c.TryGetProperty("phone_number", out var phone) && phone.GetString() == e164)
            .Select(c => new ChatwootPhoneContact(c.GetProperty("id").GetInt32(), SourceIdIn(c)))
            .FirstOrDefault();
    }

    /// <summary>Creates a contact with a phone number, in the Spirit inbox.</summary>
    /// <param name="name">The contact's name.</param>
    /// <param name="e164">The number in E.164.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The new contact; or, when Chatwoot answers that the number is taken, the contact that has it, made
    /// a moment ago by another copy or a person.
    /// </returns>
    public async Task<ChatwootPhoneContact> CreatePhoneContactAsync(string name, string e164, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["name"] = name, ["phone_number"] = e164, ["inbox_id"] = Settings.InboxId };

        using var response = await api.SendAsync(HttpMethod.Post, $"{Account}/contacts", body, cancellationToken, Settings.ServiceToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity
            && await FindPhoneContactAsync(e164, cancellationToken).ConfigureAwait(false) is { } taken)
        {
            return taken;
        }

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var created = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false)).GetProperty("payload");

        return new ChatwootPhoneContact(
            created.GetProperty("contact").GetProperty("id").GetInt32(),
            created.TryGetProperty("contact_inbox", out var inbox) && inbox.ValueKind == JsonValueKind.Object
                ? inbox.GetProperty("source_id").GetString()
                : null);
    }

    /// <summary>Gives a contact a place in the Spirit inbox.</summary>
    /// <param name="contactId">The contact's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The contact's key in the inbox.</returns>
    public async Task<string> AddContactToInboxAsync(int contactId, CancellationToken cancellationToken)
    {
        var added = await api.ReadAsync(
                HttpMethod.Post, string.Create(CultureInfo.InvariantCulture, $"{Account}/contacts/{contactId}/contact_inboxes"), new JsonObject { ["inbox_id"] = Settings.InboxId }, cancellationToken, Settings.ServiceToken)
            .ConfigureAwait(false);

        return added.GetProperty("source_id").GetString()!;
    }

    /// <summary>
    /// The contact's newest conversation in the Spirit inbox. With "Reopen same conversation" on,
    /// a contact has one.
    /// </summary>
    /// <param name="contactId">The contact's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation's display id, or null when it has none there.</returns>
    public async Task<int?> FindInboxConversationAsync(int contactId, CancellationToken cancellationToken)
    {
        var listed = await api.GetAsServiceAsync(string.Create(CultureInfo.InvariantCulture, $"{Account}/contacts/{contactId}/conversations"), cancellationToken).ConfigureAwait(false);

        return listed.GetProperty("payload").EnumerateArray()
            .Where(c => c.GetProperty("inbox_id").GetInt32() == Settings.InboxId)
            .Select(c => (int?)c.GetProperty("id").GetInt32())
            .FirstOrDefault();
    }

    /// <summary>
    /// Starts a conversation in the Spirit inbox, as the service user. While a bot is connected to
    /// the inbox Chatwoot makes it pending whatever status is asked for (probe, 2026-10-01), so
    /// <see cref="ResolveConversationAsync"/> closes it.
    /// </summary>
    /// <param name="contactId">The contact's id.</param>
    /// <param name="sourceId">The contact's key in the Spirit inbox.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The conversation's display id, and whether it is new: only a conversation with no messages is.</returns>
    public async Task<ChatwootCreatedConversation> CreateConversationAsync(int contactId, string sourceId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["source_id"] = sourceId, ["inbox_id"] = Settings.InboxId, ["contact_id"] = contactId };

        var created = await api.ReadAsync(HttpMethod.Post, $"{Account}/conversations", body, cancellationToken, Settings.ServiceToken)
            .ConfigureAwait(false);

        var fresh = created.TryGetProperty("messages", out var messages)
            && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() == 0
            && (!created.TryGetProperty("last_non_activity_message", out var last) || last.ValueKind == JsonValueKind.Null);

        return new ChatwootCreatedConversation(created.GetProperty("id").GetInt32(), fresh);
    }

    /// <summary>Resolves a conversation, as the service user, so it waits in nobody's queue.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task ResolveConversationAsync(int conversationId, CancellationToken cancellationToken)
    {
        using var response = await api.SendAsync(
                HttpMethod.Post, $"{ConversationUrl(conversationId)}/toggle_status", new JsonObject { ["status"] = "resolved" }, cancellationToken, Settings.ServiceToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a conversation already holds a note: one whose <c>source_id</c> is
    /// <paramref name="sourceId"/>, or whose text holds <paramref name="marker"/>. Reads newest
    /// first, a page at a time, and stops at the first page that reaches back past
    /// <paramref name="notBefore"/>.
    /// </summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="sourceId">The note's source id.</param>
    /// <param name="marker">A line only that note has.</param>
    /// <param name="notBefore">The note cannot be older than this, so older messages are not read.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>Whether it is there.</returns>
    public async Task<bool> HasNoteAsync(
        int conversationId, string sourceId, string marker, DateTimeOffset notBefore, CancellationToken cancellationToken)
    {
        int? before = null;

        for (var page = 0; page < MaxMessagePages; page++)
        {
            var url = $"{ConversationUrl(conversationId)}/messages"
                + (before is { } oldest ? string.Create(CultureInfo.InvariantCulture, $"?before={oldest}") : string.Empty);

            var messages = (await api.GetAsServiceAsync(url, cancellationToken).ConfigureAwait(false))
                .GetProperty("payload").EnumerateArray().ToList();

            if (messages.Any(m => TextOf(m, "source_id") == sourceId || (TextOf(m, "content")?.Contains(marker, StringComparison.Ordinal) ?? false)))
            {
                return true;
            }

            if (messages.Count < ChatwootApi.MessagePageSize
                || messages.Min(m => m.GetProperty("created_at").GetInt64()) < notBefore.ToUnixTimeSeconds())
            {
                return false;
            }

            before = messages.Min(m => m.GetProperty("id").GetInt32());
        }

        throw new InvalidOperationException($"Conversation {conversationId} has more than {MaxMessagePages} pages of messages.");
    }

    /// <summary>
    /// Whether Chatwoot's message search finds a note in the Spirit inbox that holds
    /// <paramref name="marker"/>, private notes included. One request, but the search can miss: an
    /// index lag, or a note older than its horizon. <see cref="HasNoteAsync"/> is the sure check.
    /// </summary>
    /// <param name="marker">A line only that note has.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it is there.</returns>
    public async Task<bool> HasCallNoteAsync(string marker, CancellationToken cancellationToken)
    {
        var found = await api.GetAsServiceAsync($"{Account}/search/messages?q={Uri.EscapeDataString(marker)}", cancellationToken)
            .ConfigureAwait(false);

        return found.GetProperty("payload").GetProperty("messages").EnumerateArray()
            .Any(m => m.GetProperty("inbox_id").GetInt32() == Settings.InboxId
                && (TextOf(m, "content")?.Contains(marker, StringComparison.Ordinal) ?? false));
    }

    /// <summary>Posts a private note, which only staff see, as the bot.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="content">The note.</param>
    /// <param name="sourceId">The id <see cref="HasNoteAsync"/> finds it by, or <see langword="null"/> for a note nobody looks up.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task PostNoteAsync(int conversationId, string content, string? sourceId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["content"] = content, ["message_type"] = "outgoing", ["private"] = true };

        if (sourceId is not null)
        {
            body["source_id"] = sourceId;
        }

        using var response = await api.SendAsync(HttpMethod.Post, $"{ConversationUrl(conversationId)}/messages", body, cancellationToken)
            .ConfigureAwait(false);

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static ChatwootTeam ReadTeam(JsonElement team) => new(
        team.GetProperty("id").GetInt32(),
        team.GetProperty("name").GetString() ?? string.Empty,
        team.GetProperty("description").GetString() ?? string.Empty);

    private string? SourceIdIn(JsonElement contact)
        => contact.TryGetProperty("contact_inboxes", out var inboxes) && inboxes.ValueKind == JsonValueKind.Array
            ? inboxes.EnumerateArray()
                .Where(i => i.GetProperty("inbox").GetProperty("id").GetInt32() == Settings.InboxId)
                .Select(i => i.GetProperty("source_id").GetString())
                .FirstOrDefault()
            : null;

    private static string? TextOf(JsonElement message, string name)
        => message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private string ConversationUrl(int conversationId) => api.ConversationUrl(conversationId);

    private string ContactUrl(string sourceId)
        => $"{Settings.BaseUrl.TrimEnd('/')}/public/api/v1/inboxes/{Settings.InboxIdentifier}/contacts/{Uri.EscapeDataString(sourceId)}";

    /// <summary>Chatwoot answers a refused change with <c>422</c> and <c>{"message": …}</c>.</summary>
    private async Task<ChatwootContactUpdate> UpdateContactAsync(int contactId, JsonObject body, CancellationToken cancellationToken)
    {
        using var response = await api.SendAsync(
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

        await ChatwootApi.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        return ChatwootContactUpdate.Saved;
    }
}
