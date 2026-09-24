using System.Text.Json;

namespace SpiritAI.Chatwoot;

/// <summary>One message of a conversation, as the visitor's side of Chatwoot shows it.</summary>
/// <param name="Id">The message's id. A later message has a larger id.</param>
/// <param name="Content">The words, or <see langword="null"/> for a message with only attachments.</param>
/// <param name="MessageType"><c>0</c> incoming, <c>1</c> outgoing, <c>2</c> activity, <c>3</c> template.</param>
/// <param name="SenderType">
/// <c>contact</c>, <c>user</c> (staff), or <c>agent_bot</c>; <see langword="null"/> for an activity.
/// </param>
/// <param name="SenderName">The sender's name, such as the member of staff who wrote.</param>
public sealed record ChatwootMessage(int Id, string? Content, int MessageType, string? SenderType, string? SenderName)
{
    internal static ChatwootMessage Read(JsonElement message)
    {
        var sender = message.TryGetProperty("sender", out var s) && s.ValueKind == JsonValueKind.Object ? s : (JsonElement?)null;

        return new ChatwootMessage(
            message.GetProperty("id").GetInt32(),
            message.GetProperty("content").GetString(),
            message.GetProperty("message_type").GetInt32(),
            sender?.GetProperty("type").GetString(),
            sender?.GetProperty("name").GetString());
    }
}
