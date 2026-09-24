using System.Text.Json;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The fields Spirit reads off one Chatwoot inbox webhook, from whichever place the event puts
/// them. A message event nests the conversation under <c>conversation</c>; a conversation event
/// is the conversation; a typing event names the person under <c>user</c>.
/// </summary>
/// <param name="Name">The event, such as <c>message_created</c>.</param>
/// <param name="SpiritConversationId">The AgentCore conversation the Chatwoot conversation mirrors, or <see langword="null"/> when it names none.</param>
/// <param name="ChatwootConversationId">The Chatwoot conversation's display id, which its API routes use.</param>
/// <param name="Status">The Chatwoot conversation's status: <c>open</c>, <c>pending</c>, <c>resolved</c>, or <c>snoozed</c>.</param>
/// <param name="AssigneeName">The person the conversation is assigned to, or <see langword="null"/> when nobody is.</param>
/// <param name="MessageType">A message event's <c>message_type</c>: <c>incoming</c>, <c>outgoing</c>, <c>activity</c>, or <c>template</c>.</param>
/// <param name="IsPrivate">Whether a message is a private note, or a typing event is in the note box.</param>
/// <param name="Content">A message event's text.</param>
/// <param name="ActorType">Who acted: a message's sender, or the typing person. <c>user</c> for a member of staff.</param>
/// <param name="ActorId">The actor's Chatwoot id.</param>
/// <param name="ActorName">The actor's name.</param>
public sealed record ChatwootEvent(
    string Name,
    string? SpiritConversationId,
    int? ChatwootConversationId,
    string? Status,
    string? AssigneeName,
    string? MessageType,
    bool IsPrivate,
    string? Content,
    string? ActorType,
    long? ActorId,
    string? ActorName)
{
    /// <summary>The Chatwoot conversation custom attribute that holds the AgentCore conversation id.</summary>
    public const string SpiritConversationAttribute = "spirit_conversation_id";

    /// <summary>The <see cref="ActorType"/> of a member of staff.</summary>
    public const string StaffActor = "user";

    /// <summary>Reads an event off a webhook body.</summary>
    /// <param name="body">The parsed body.</param>
    /// <returns>The event, or <see langword="null"/> when the body is not an event.</returns>
    public static ChatwootEvent? Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || Text(body, "event") is not { } name)
        {
            return null;
        }

        var isConversationEvent = name.StartsWith("conversation_", StringComparison.Ordinal)
            && !name.StartsWith("conversation_typing_", StringComparison.Ordinal);

        var conversation = isConversationEvent ? body : Child(body, "conversation");
        var meta = Child(conversation, "meta");
        var actor = name.StartsWith("conversation_typing_", StringComparison.Ordinal) ? Child(body, "user") : Child(body, "sender");

        var assignee = Child(meta, "assignee");
        var assigneeIsBot = string.Equals(Text(meta, "assignee_type"), "AgentBot", StringComparison.Ordinal);

        return new ChatwootEvent(
            name,
            Text(Child(conversation, "custom_attributes"), SpiritConversationAttribute),
            Number(conversation, "id") is { } id ? (int)id : null,
            Text(conversation, "status"),
            assigneeIsBot ? null : Text(assignee, "name"),
            isConversationEvent ? null : Text(body, "message_type"),
            Flag(body, "private") || Flag(body, "is_private"),
            isConversationEvent ? null : Text(body, "content"),
            Text(actor, "type"),
            Number(actor, "id"),
            Text(actor, "name"));
    }

    private static JsonElement Child(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var child) ? child : default;

    private static string? Text(JsonElement parent, string name)
        => Child(parent, name) is { ValueKind: JsonValueKind.String } value && value.GetString() is { Length: > 0 } text ? text : null;

    private static long? Number(JsonElement parent, string name)
        => Child(parent, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) ? number : null;

    private static bool Flag(JsonElement parent, string name)
        => Child(parent, name).ValueKind == JsonValueKind.True;
}
