namespace SpiritAI.Handoffs.RealTime;

/// <summary>Runs <see cref="StaffPresenceWatch.CheckAsync"/> every <see cref="StaffPresenceWatch.Interval"/>.</summary>
internal sealed class StaffPresenceWorker(StaffPresenceWatch watch, TimeProvider clock) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(StaffPresenceWatch.Interval, clock);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await watch.CheckAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }
}
