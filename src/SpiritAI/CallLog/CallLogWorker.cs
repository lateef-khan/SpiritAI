namespace SpiritAI.CallLog;

/// <summary>Copies each queued call, one at a time, each in a scope of its own.</summary>
public sealed class CallLogWorker(CallLogQueue queue, IServiceScopeFactory scopes, ILogger<CallLogWorker> logger) : BackgroundService
{
    /// <summary>How many calls pass between two progress lines.</summary>
    public const int ProgressEvery = 500;

    private readonly CallLogTally tally = new();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var callId in queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                tally.Add(await CopyOnceAsync(callId, stoppingToken).ConfigureAwait(false));
                LogProgress();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>A failed copy is logged and the next call goes on; the next start's catch-up tries it again.</summary>
    /// <returns>What the copy did, or null when it failed.</returns>
    private async Task<CallLogResult?> CopyOnceAsync(string callId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<CallLogCopier>().CopyAsync(callId, cancellationToken).ConfigureAwait(false);

            // A backfill skips thousands of calls; only the ones that did something are worth a line each.
            var level = result is CallLogResult.Copied or CallLogResult.NotReady ? LogLevel.Information : LogLevel.Debug;
            logger.Log(level, "Call log {Result} for call {ConversationSpaceId}.", result, callId);

            return result;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Call log failed on call {ConversationSpaceId}.", callId);

            return null;
        }
    }

    private void LogProgress()
    {
        var waiting = queue.Waiting;

        if (tally.Processed % ProgressEvery == 0)
        {
            logger.LogInformation(
                "Call log progress: {Processed} processed ({Copied} copied, {AlreadyCopied} already copied, {Skipped} skipped, {NotReady} not ready, {Failed} failed), {Waiting} waiting.",
                [.. tally.Counts(), waiting]);
        }

        // Each call is taken off the queue before it is copied, so none waiting means the queue ran dry.
        if (waiting == 0)
        {
            logger.LogInformation(
                "Call log queue empty: {Processed} processed ({Copied} copied, {AlreadyCopied} already copied, {Skipped} skipped, {NotReady} not ready, {Failed} failed).",
                tally.Counts());
        }
    }
}
