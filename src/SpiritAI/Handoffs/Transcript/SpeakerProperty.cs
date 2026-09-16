using System.Text.Json;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Transcript;

/// <summary>
/// The <c>speaker</c> entry of a message's <see cref="ChatMessage.AdditionalProperties"/>: who
/// wrote a message of the human phase, in the shape section 4.3 of the spec lays out.
/// </summary>
/// <remarks>
/// It is written and read as a <see cref="JsonElement"/> rather than as the record. The row
/// round-trips through AgentCore's JSON and comes back untyped, so an element is the one shape a
/// stored message and a freshly built one have in common.
/// </remarks>
public static class SpeakerProperty
{
    /// <summary>The key under which the speaker is filed.</summary>
    public const string Name = "speaker";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Puts the speaker on a message, replacing any it carried.</summary>
    /// <param name="message">The message.</param>
    /// <param name="speaker">Who wrote it.</param>
    public static void Attach(ChatMessage message, HandoffSpeaker speaker)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(speaker);

        message.AdditionalProperties ??= [];
        message.AdditionalProperties[Name] = JsonSerializer.SerializeToElement(speaker, Json);
    }

    /// <summary>Reads the speaker off a message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The speaker as stored, or <see langword="null"/> when the message names none.</returns>
    public static JsonElement? Read(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.AdditionalProperties?.TryGetValue(Name, out var value) == true
            && value is JsonElement { ValueKind: JsonValueKind.Object } speaker
            ? speaker
            : null;
    }
}
