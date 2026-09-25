using System.Text.Json;

namespace SpiritAI.GoTo;

/// <summary>
/// Takes each call event off the <see cref="GoToCallEventQueue"/>. For now it only logs which call
/// the event is about.
/// </summary>
public sealed class GoToCallEventReader(GoToCallEventQueue queue, ILogger<GoToCallEventReader> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var callEvent in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                // An event also carries phone numbers and names. Only kinds and ids go in the log.
                logger.LogInformation(
                    "GoTo call event {Type} ({State}) for call {ConversationSpaceId}.",
                    Text(callEvent, "type"),
                    Text(callEvent, "content", "state", "type"),
                    Text(callEvent, "content", "metadata", "conversationSpaceId"));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>The string at <paramref name="path"/>, or null when the event has no such field.</summary>
    private static string? Text(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
