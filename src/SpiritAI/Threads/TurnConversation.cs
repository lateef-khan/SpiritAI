using System.Text.Json;

namespace SpiritAI.Threads;

/// <summary>
/// Reads the conversation a Responses turn runs under, off the body that names it.
/// </summary>
public static class TurnConversation
{
    /// <summary>Reads the turn's conversation.</summary>
    /// <param name="request">The turn, whose body may be anything at all.</param>
    /// <returns>The conversation id, or empty when the turn names none.</returns>
    public static async Task<string> ReadAsync(HttpRequest request)
    {
        JsonElement body;
        try
        {
            request.EnableBuffering();

            body = await JsonSerializer
                .DeserializeAsync<JsonElement>(request.Body, cancellationToken: request.HttpContext.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }

        if (body.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (body.TryGetProperty("conversation", out var conversation))
        {
            if (conversation.ValueKind == JsonValueKind.String
                && conversation.GetString() is { Length: > 0 } id)
            {
                return id;
            }

            if (conversation.ValueKind == JsonValueKind.Object
                && conversation.TryGetProperty("id", out var nested)
                && nested.ValueKind == JsonValueKind.String
                && nested.GetString() is { Length: > 0 } nestedId)
            {
                return nestedId;
            }
        }

        return string.Empty;
    }
}
