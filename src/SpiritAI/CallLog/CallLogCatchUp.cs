using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Options;

using SpiritAI.GoTo;

namespace SpiritAI.CallLog;

/// <summary>
/// Once per start, queues every call that ended since <see cref="CallLogOptions.CatchUpFrom"/>, or
/// in the last <see cref="CallLogOptions.CatchUpWindow"/>.
/// </summary>
public sealed class CallLogCatchUp(
    IServiceScopeFactory scopes,
    CallLogQueue queue,
    IOptions<CallLogOptions> options,
    TimeProvider clock,
    ILogger<CallLogCatchUp> logger) : BackgroundService
{
    private static readonly TimeSpan LongBackfill = TimeSpan.FromDays(7);

    /// <summary>
    /// Walks GoTo's call list one day at a time, oldest first, and queues each call worth a note.
    /// </summary>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <returns>How many calls it queued.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var goTo = scope.ServiceProvider.GetRequiredService<GoToClient>();

        var now = clock.GetUtcNow();
        var from = options.Value.CatchUpFrom ?? now - options.Value.CatchUpWindow;
        var queued = 0;

        WarnOfALongBackfill(from, now);

        for (var start = from; start < now; start = start.AddDays(1))
        {
            queued += await QueueDayAsync(goTo, start, start.AddDays(1) < now ? start.AddDays(1) : now, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("The call log catch-up queued {Count} calls since {From}.", queued, from);

        return queued;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The call log catch-up stopped; the next start runs it again.");
        }
    }

    private void WarnOfALongBackfill(DateTimeOffset from, DateTimeOffset now)
    {
        if (options.Value.CatchUpFrom is not null && from < now - LongBackfill)
        {
            logger.LogWarning(
                "CallLog:CatchUpFrom is {From}: every start re-checks every call since then. Remove it once a \"The call log catch-up queued N calls\" line is followed by a \"Call log queue empty\" line.",
                from);
        }
    }

    /// <summary>Queues one window's calls. A window GoTo fails on is logged, and the walk goes on to the next.</summary>
    private async Task<int> QueueDayAsync(GoToClient goTo, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var queued = 0;

        try
        {
            await foreach (var callId in CallsWorthANoteAsync(goTo, start, end, cancellationToken).ConfigureAwait(false))
            {
                queue.Add(callId);
                queued++;
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception, "The call log catch-up skipped {Start} to {End}; the next start tries it again.", start.UtcDateTime, end.UtcDateTime);
        }

        return queued;
    }

    /// <summary>Every page of one window's calls; a page marker that never runs out stops at <see cref="GoToApi.MaxPages"/>.</summary>
    private static async IAsyncEnumerable<string> CallsWorthANoteAsync(
        GoToClient goTo, DateTimeOffset start, DateTimeOffset end, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? marker = null;

        for (var pages = 0; pages < GoToApi.MaxPages; pages++)
        {
            var page = await goTo.ListReportSummariesAsync(start, end, marker, cancellationToken).ConfigureAwait(false);

            foreach (var call in page.Calls.Where(c => c.MayBeLogged))
            {
                yield return call.Id;
            }

            marker = page.NextPageMarker;

            if (marker is null)
            {
                yield break;
            }
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"GoTo listed more than {GoToApi.MaxPages} pages of calls from {start.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'} to {end.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}."));
    }
}
