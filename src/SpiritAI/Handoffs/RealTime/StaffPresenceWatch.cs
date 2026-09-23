using SpiritAI.Handoffs.Desk;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;

namespace SpiritAI.Handoffs.RealTime;

/// <summary>
/// Tells the widgets how many staff are online when that changes.
/// </summary>
public sealed class StaffPresenceWatch(
    IServiceScopeFactory scopes,
    IRealTimePublisher publisher,
    ILogger<StaffPresenceWatch> logger)
{
    /// <summary>
    /// How often <see cref="StaffPresenceWorker"/> checks. Half the life of the count kept by
    /// <c>ChatwootStaffPresence</c>: a check as long as that life lands just before each
    /// expiry and reads the old count, so a change would wait a whole extra minute.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private int? _told;

    /// <summary>
    /// One check. A failure is logged and the next check tries again: the database may be asleep.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();

            var visitors = await scope.ServiceProvider.GetRequiredService<IPresenceStore>()
                .CountOnlineAsync(HandoffAdmission.VisitorKind, cancellationToken)
                .ConfigureAwait(false);

            if (visitors == 0)
            {
                _told = null;
                return;
            }

            var online = await scope.ServiceProvider.GetRequiredService<IStaffPresence>()
                .CountOnlineAsync(cancellationToken)
                .ConfigureAwait(false);

            if (online == _told)
            {
                return;
            }

            await publisher
                .PublishAsync(
                    RealTimeGroups.Presence,
                    RealTimeEvents.Presence,
                    new RealTimePresence(HandoffAdmission.StaffKind, online),
                    cancellationToken)
                .ConfigureAwait(false);

            _told = online;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Checking who of staff is online failed; trying again next tick.");
        }
    }
}
