using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Reads the rows out of whatever a DAB tool wrapped them in.
/// </summary>
/// <remarks>
/// Every reader of these tools meets the same three envelopes, so the unwrapping lives once here
/// rather than in each of them.
/// </remarks>
internal static class DabEnvelope
{
    /// <summary>Finds the rows in whichever envelope a DAB tool wrapped them in.</summary>
    /// <remarks>
    /// A stored procedure answers <c>{ status, value: { value: [...] } }</c> and
    /// <c>read_records</c> answers <c>{ result: { value: [...] } }</c>. A refusal answers
    /// <c>{ status: "error", error: { ... } }</c> and is read here as nothing.
    /// </remarks>
    /// <param name="payload">What the tool answered.</param>
    /// <returns>The rows, or <see langword="null"/> when the payload holds none.</returns>
    internal static IReadOnlyList<JsonElement>? RowsOf(JsonElement payload)
    {
        var body = Unwrap(payload);

        if (body.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (body.TryGetProperty("status", out var status)
            && status.ValueKind == JsonValueKind.String
            && string.Equals(status.GetString(), "error", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var outer in (string[])["value", "result"])
        {
            if (body.TryGetProperty(outer, out var wrapper)
                && wrapper.ValueKind == JsonValueKind.Object
                && wrapper.TryGetProperty("value", out var rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                return [.. rows.EnumerateArray()];
            }
        }

        return null;
    }

    /// <summary>Digs the JSON body out of whatever the tool layer handed back.</summary>
    /// <remarks>
    /// An MCP tool answers with content parts, and the part carrying the rows is a string of JSON.
    /// Depending on how the call was made, that arrives already parsed, as that string, or still
    /// inside its <c>content</c> array. All three are the same body.
    /// </remarks>
    /// <param name="payload">What the tool answered.</param>
    /// <returns>The body, parsed.</returns>
    internal static JsonElement Unwrap(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array
            && content.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } part
            && part.TryGetProperty("text", out var text))
        {
            return Unwrap(text);
        }

        if (payload.ValueKind != JsonValueKind.String)
        {
            return payload;
        }

        try
        {
            using var parsed = JsonDocument.Parse(payload.GetString() ?? string.Empty);

            return parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return payload;
        }
    }
}
