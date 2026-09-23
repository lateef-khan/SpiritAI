namespace SpiritAI.Chatwoot;

/// <summary>
/// Copies queued chats one at a time, so two copies of one chat never race to open its Chatwoot
/// conversation. A failed copy costs a log line; the chat's next turn tries again from where the
/// last copy stopped.
/// </summary>
internal sealed class ChatwootCopyWorker(
    ChatwootCopyQueue queue,
    IServiceScopeFactory scopes,
    ILogger<ChatwootCopyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var conversationId in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();

                await scope.ServiceProvider.GetRequiredService<ChatwootCopy>()
                    .CopyAsync(conversationId, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogError(failure, "Copying conversation {ConversationId} into Chatwoot failed.", conversationId);
            }
        }
    }
}
