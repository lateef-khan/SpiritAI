namespace SpiritAI.Chatwoot;

/// <summary>
/// Sends the visitor's typing to Chatwoot one signal at a time. A failed send costs a log line:
/// typing is a hint.
/// </summary>
internal sealed class ChatwootTypingWorker(
    ChatwootTypingQueue queue,
    IServiceScopeFactory scopes,
    ILogger<ChatwootTypingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var typing in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();

                await scope.ServiceProvider.GetRequiredService<ChatwootTypingSender>()
                    .SendAsync(typing, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogWarning(failure, "Telling Chatwoot that the visitor of {ConversationId} is typing failed.", typing.ConversationId);
            }
        }
    }
}
