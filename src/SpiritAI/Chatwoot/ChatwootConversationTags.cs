using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Labels and custom fields on conversations, and the search over them. Chatwoot replaces the
/// whole list on each write, so each change reads the list first and keeps what staff set.
/// </summary>
public sealed class ChatwootConversationTags(HttpClient http, IOptions<ChatwootOptions> options)
{
    private readonly ChatwootApi api = new(http, options);

    /// <summary>Adds one label to a conversation, as the bot. The label need not be defined.</summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="label">The label, in lowercase.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task AddLabelAsync(int conversationId, string label, CancellationToken cancellationToken)
    {
        var labels = await LabelsAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if (!labels.Contains(label))
        {
            await WriteLabelsAsync(conversationId, [.. labels, label], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets one custom field on a conversation, as the bot. Chatwoot keeps the value only when the
    /// field is defined for conversations.
    /// </summary>
    /// <param name="conversationId">The conversation's display id.</param>
    /// <param name="key">The field's key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task SetFieldAsync(int conversationId, string key, string value, CancellationToken cancellationToken)
    {
        var shown = await api.ReadAsync(HttpMethod.Get, api.ConversationUrl(conversationId), body: null, cancellationToken)
            .ConfigureAwait(false);

        var fields = shown.TryGetProperty("custom_attributes", out var kept) && kept.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(kept.GetRawText())!.AsObject()
            : [];

        fields[key] = value;

        await api.WriteAsync(
                HttpMethod.Post,
                $"{api.ConversationUrl(conversationId)}/custom_attributes",
                new JsonObject { ["custom_attributes"] = fields },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The conversations whose custom field <paramref name="key"/> is exactly <paramref name="value"/>
    /// and that carry <paramref name="label"/>, as the service user: the bot cannot search.
    /// </summary>
    /// <param name="key">The custom field's key.</param>
    /// <param name="value">The value to match.</param>
    /// <param name="label">The label to match.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The display ids of the first page, up to 25; empty when none match.</returns>
    public async Task<IReadOnlyList<int>> FindAsync(string key, string value, string label, CancellationToken cancellationToken)
    {
        var filter = new JsonObject
        {
            ["payload"] = new JsonArray(
                Condition(key, value, "and"),
                Condition("labels", label, queryOperator: null)),
        };

        var found = await api.ReadAsync(
                HttpMethod.Post, $"{api.Account}/conversations/filter?page=1", filter, cancellationToken, api.Settings.ServiceToken)
            .ConfigureAwait(false);

        return [.. found.GetProperty("payload").EnumerateArray().Select(c => c.GetProperty("id").GetInt32())];
    }

    private static JsonObject Condition(string key, string value, string? queryOperator) => new()
    {
        ["attribute_key"] = key,
        ["filter_operator"] = "equal_to",
        ["values"] = new JsonArray(value),
        ["query_operator"] = queryOperator,
    };

    private async Task<IReadOnlyList<string>> LabelsAsync(int conversationId, CancellationToken cancellationToken)
    {
        var shown = await api.ReadAsync(HttpMethod.Get, $"{api.ConversationUrl(conversationId)}/labels", body: null, cancellationToken)
            .ConfigureAwait(false);

        return [.. shown.GetProperty("payload").EnumerateArray().Select(l => l.GetString()!)];
    }

    private async Task WriteLabelsAsync(int conversationId, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["labels"] = new JsonArray([.. labels.Select(l => JsonValue.Create(l))]) };

        await api.WriteAsync(HttpMethod.Post, $"{api.ConversationUrl(conversationId)}/labels", body, cancellationToken)
            .ConfigureAwait(false);
    }
}
