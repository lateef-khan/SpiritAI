using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The Chatwoot ids of an AgentCore conversation, kept in its <c>custom</c> column next to the
/// bookmark, so a tool can act on the conversation's Chatwoot side from its id alone.
/// </summary>
/// <param name="ConversationId">The Chatwoot conversation's display id.</param>
/// <param name="VisitorKey">The visitor's key: their contact's <c>source_id</c> in the inbox.</param>
public sealed record ChatwootIds(int ConversationId, string VisitorKey)
{
    /// <summary>The key in <see cref="ChatwootBookmark.Section"/> that holds the display id.</summary>
    public const string ConversationKey = "conversation";

    /// <summary>The key in <see cref="ChatwootBookmark.Section"/> that holds the visitor's key.</summary>
    public const string VisitorKeyKey = "visitor";

    /// <summary>Reads the ids.</summary>
    /// <param name="custom">The conversation's <c>custom</c> column.</param>
    /// <returns>The ids, or <see langword="null"/> when the conversation is not a Chatwoot one.</returns>
    public static ChatwootIds? Read(JsonElement? custom)
        => custom is { ValueKind: JsonValueKind.Object } root
            && root.TryGetProperty(ChatwootBookmark.Section, out var section)
            && section.ValueKind == JsonValueKind.Object
            && section.TryGetProperty(ConversationKey, out var conversation)
            && conversation.TryGetInt32(out var conversationId)
            && section.TryGetProperty(VisitorKeyKey, out var visitor)
            && visitor.GetString() is { Length: > 0 } visitorKey
                ? new ChatwootIds(conversationId, visitorKey)
                : null;

    /// <summary>Writes the ids, and keeps every other key of <paramref name="custom"/>.</summary>
    /// <param name="custom">The conversation's <c>custom</c> column.</param>
    /// <returns>The new <c>custom</c> column.</returns>
    public JsonElement Write(JsonElement? custom)
    {
        var root = custom is { ValueKind: JsonValueKind.Object } kept ? JsonObject.Create(kept)! : [];

        if (root[ChatwootBookmark.Section] is not JsonObject section)
        {
            section = [];
            root[ChatwootBookmark.Section] = section;
        }

        section[ConversationKey] = ConversationId;
        section[VisitorKeyKey] = VisitorKey;

        return JsonSerializer.SerializeToElement(root);
    }
}
