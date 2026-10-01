using System.Text.Json.Nodes;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>The saved Chatwoot replies, and the ones built from them.</summary>
internal static class ChatwootPayloads
{
    /// <summary>The create answer Chatwoot gives when the contact already has a conversation: the saved fresh one, with a message in it.</summary>
    public static string ConversationCreatedWithAMessage()
    {
        var answer = JsonNode.Parse(Read("phone_conversation_created"))!;
        answer["messages"] = new JsonArray(new JsonObject { ["id"] = 1, ["content"] = "an earlier message", ["conversation_id"] = 31 });

        return answer.ToJsonString();
    }

    public static string Read(string payload)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Chatwoot", "Payloads", payload + ".json"));
}
