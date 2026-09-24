using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The <c>chatwoot.through</c> entry of an AgentCore conversation's <c>custom</c> column: the id
/// of the newest Chatwoot message the AgentCore copy already has.
/// </summary>
public static class ChatwootBookmark
{
    /// <summary>The key in <c>custom</c> that holds Spirit's Chatwoot entries.</summary>
    public const string Section = "chatwoot";

    /// <summary>The key in <see cref="Section"/> that holds the bookmark.</summary>
    public const string Through = "through";

    /// <summary>Reads the bookmark.</summary>
    /// <param name="custom">The conversation's <c>custom</c> column.</param>
    /// <returns>The message id, or <see langword="null"/> when the copy has none, as after a sweep.</returns>
    public static int? Read(JsonElement? custom)
        => custom is { ValueKind: JsonValueKind.Object } root
            && root.TryGetProperty(Section, out var section)
            && section.ValueKind == JsonValueKind.Object
            && section.TryGetProperty(Through, out var through)
            && through.TryGetInt32(out var id)
                ? id
                : null;

    /// <summary>Moves the bookmark, and keeps every other key of <paramref name="custom"/>.</summary>
    /// <param name="custom">The conversation's <c>custom</c> column.</param>
    /// <param name="through">The newest Chatwoot message id the copy has.</param>
    /// <returns>The new <c>custom</c> column.</returns>
    public static JsonElement Write(JsonElement? custom, int through)
    {
        var root = custom is { ValueKind: JsonValueKind.Object } kept ? JsonObject.Create(kept)! : [];

        if (root[Section] is not JsonObject section)
        {
            section = [];
            root[Section] = section;
        }

        section[Through] = through;

        return JsonSerializer.SerializeToElement(root);
    }
}
