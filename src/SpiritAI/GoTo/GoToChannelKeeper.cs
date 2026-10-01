using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>
/// Keeps Spirit's GoTo webhook channel and its call-events and call-report subscriptions alive.
/// </summary>
public sealed class GoToChannelKeeper(
    IServiceScopeFactory scopes,
    IOptions<GoToOptions> options,
    IHostApplicationLifetime lifetime,
    TimeProvider clock,
    ILogger<GoToChannelKeeper> logger) : BackgroundService
{
    /// <summary>How often the channel is checked.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Deletes each channel with Spirit's nickname and another URL, makes the channel when none is
    /// left, and subscribes it to calls and to reports when a subscription does not show the account.
    /// </summary>
    /// <param name="cancellationToken">Stops the run between calls.</param>
    public async Task KeepOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.TryGetWebhookUrl(out var webhookUrl))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var goTo = scope.ServiceProvider.GetRequiredService<GoToClient>();

        var ours = (await goTo.ListChannelsAsync(cancellationToken).ConfigureAwait(false))
            .Where(c => c.Nickname == settings.ChannelNickname)
            .ToList();

        var kept = ours.FirstOrDefault(c => c.WebhookUrl == webhookUrl.AbsoluteUri);

        foreach (var stale in ours.Where(c => !ReferenceEquals(c, kept)))
        {
            await DeleteAndLogAsync(goTo, stale, cancellationToken).ConfigureAwait(false);
        }

        if (kept is null)
        {
            kept = await goTo.CreateWebhookChannelAsync(settings.ChannelNickname, webhookUrl, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Made GoTo channel {ChannelId} ({Nickname}).", kept.ChannelId, kept.Nickname);
        }

        var channelId = kept.ChannelId;

        await KeepSubscriptionAsync("call-events", channelId, () => KeepCallEventsAsync(goTo, channelId, settings.AccountKey, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        await KeepSubscriptionAsync("call-report", channelId, () => KeepCallReportsAsync(goTo, channelId, settings.AccountKey, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.TryGetWebhookUrl(out _))
        {
            logger.LogInformation(
                "The GoTo channel job is off: set {Section}:WebhookBaseUrl, WebhookSecret and ChannelNickname to turn it on.",
                GoToOptions.SectionName);

            return;
        }

        logger.LogInformation(
            "The GoTo channel job keeps channel {Nickname} pointed at {BaseUrl}.",
            settings.ChannelNickname,
            settings.WebhookBaseUrl);

        try
        {
            // A create makes GoTo send OPTIONS to this server, so it must be listening first.
            await WhenStartedAsync(stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(Interval, clock);

            do
            {
                await KeepAndLogAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>A failed run is logged and tried again on the next tick.</summary>
    private async Task KeepAndLogAsync(CancellationToken cancellationToken)
    {
        try
        {
            await KeepOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The GoTo channel job failed; trying again next tick.");
        }
    }

    /// <summary>One subscription that fails to read or subscribe is logged and does not stop the other.</summary>
    private async Task KeepSubscriptionAsync(string subscription, string channelId, Func<Task> keep, CancellationToken cancellationToken)
    {
        try
        {
            await keep().ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not keep the {Subscription} subscription of GoTo channel {ChannelId}.", subscription, channelId);
        }
    }

    private async Task KeepCallEventsAsync(GoToClient goTo, string channelId, string accountKey, CancellationToken cancellationToken)
    {
        // Read before subscribing, never after: GoTo's read lags a subscribe by a second or two.
        var accounts = await goTo.ReadSubscribedAccountKeysAsync(channelId, cancellationToken).ConfigureAwait(false);

        if (!accounts.Contains(accountKey))
        {
            logger.LogWarning("GoTo channel {ChannelId} has no call-events subscription for the account; subscribing.", channelId);

            await goTo.SubscribeToCallEventsAsync(channelId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task KeepCallReportsAsync(GoToClient goTo, string channelId, string accountKey, CancellationToken cancellationToken)
    {
        var accounts = await goTo.ReadReportSubscribedAccountKeysAsync(channelId, cancellationToken).ConfigureAwait(false);

        if (!accounts.Contains(accountKey))
        {
            logger.LogWarning("GoTo channel {ChannelId} has no call-report subscription for the account; subscribing.", channelId);

            await goTo.SubscribeToCallReportsAsync(channelId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>One failed delete does not stop the others, or the create after them.</summary>
    private async Task DeleteAndLogAsync(
        GoToClient goTo, GoToChannel stale, CancellationToken cancellationToken)
    {
        try
        {
            await goTo.DeleteChannelAsync(stale.Nickname, stale.ChannelId, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Deleted stale GoTo channel {ChannelId} ({Nickname}).", stale.ChannelId, stale.Nickname);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not delete stale GoTo channel {ChannelId}.", stale.ChannelId);
        }
    }

    private async Task WhenStartedAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var onStarted = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using var onStopping = stoppingToken.Register(() => started.TrySetCanceled(stoppingToken));

        await started.Task.ConfigureAwait(false);
    }
}
