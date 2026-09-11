using Microsoft.Extensions.Options;

namespace SpiritAI.RealTime.Presence;

/// <summary>
/// Says goodbye for the sockets that could not: every heartbeat it deletes the presence rows not
/// seen inside the window, and for each kind that lost one, tells everyone the new count.
/// </summary>
internal sealed class PresenceSweeper(
    IServiceScopeFactory scopes,
    IRealTimePublisher publisher,
    IOptions<RealTimeOptions> options,
    TimeProvider clock,
    ILogger<PresenceSweeper> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.HeartbeatSeconds), clock);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>
    /// One tick. A failure is logged and the loop goes on: the database may be asleep, and the
    /// next tick will find it awake.
    /// </summary>
    private async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var presence = scope.ServiceProvider.GetRequiredService<IPresenceStore>();

            foreach (var kind in await presence.SweepAsync(cancellationToken).ConfigureAwait(false))
            {
                var online = await presence.CountOnlineAsync(kind, cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Swept stale {Kind} socket(s); {Online} online.", kind, online);

                await publisher
                    .PublishAsync(RealTimeGroups.Presence, RealTimeEvents.Presence, new RealTimePresence(kind, online), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The presence sweep failed; trying again next tick.");
        }
    }
}
