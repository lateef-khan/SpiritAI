namespace SpiritAI.Chatwoot;

/// <summary>
/// Handles queued webhooks one at a time, so two events of one chat never race. A failed event
/// costs a log line and never the ones behind it.
/// </summary>
internal sealed class ChatwootEventWorker(
    ChatwootEventQueue queue,
    IServiceScopeFactory scopes,
    ILogger<ChatwootEventWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var e in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();

                await scope.ServiceProvider.GetRequiredService<ChatwootEventHandler>()
                    .HandleAsync(e, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogError(
                    failure,
                    "Chatwoot event {Event} on Chatwoot conversation {ChatwootConversationId} failed.",
                    e.Name,
                    e.ChatwootConversationId);
            }
        }
    }
}
