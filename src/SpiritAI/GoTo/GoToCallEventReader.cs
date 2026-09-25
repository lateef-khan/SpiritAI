using System.Text.Json;

namespace SpiritAI.GoTo;

/// <summary>
/// Takes each call event off the <see cref="GoToCallEventQueue"/>, logs which call it is about, and
/// hands a call with staff lines in it to every <see cref="IGoToCallHandler"/>.
/// </summary>
public sealed class GoToCallEventReader(
    GoToCallEventQueue queue,
    IServiceScopeFactory scopes,
    ILogger<GoToCallEventReader> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var callEvent in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await ReadOnceAsync(callEvent, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task ReadOnceAsync(JsonElement callEvent, CancellationToken cancellationToken)
    {
        var call = GoToCall.Read(callEvent);

        // An event also carries phone numbers and names. Only kinds and ids go in the log.
        logger.LogInformation(
            "GoTo call event {State} for call {ConversationSpaceId} with {Lines} staff lines.",
            call?.State,
            call?.Id,
            call?.Lines.Count ?? 0);

        if (call is not { Lines.Count: > 0 })
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();

        foreach (var handler in scope.ServiceProvider.GetServices<IGoToCallHandler>())
        {
            try
            {
                await handler.HandleAsync(call, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "{Handler} failed on call {ConversationSpaceId}.", handler.GetType().Name, call.Id);
            }
        }
    }
}
