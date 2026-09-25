using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>
/// Keeps Spirit's GoTo webhook channel and its call-events subscription alive.
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
    /// left, and subscribes it when its subscription does not show the account.
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
        var channels = scope.ServiceProvider.GetRequiredService<IGoToNotificationChannelApiClient>();
        var callEvents = scope.ServiceProvider.GetRequiredService<IGoToCallEventsApiClient>();

        var ours = (await channels.ListChannelsAsync(cancellationToken).ConfigureAwait(false))
            .Where(c => c.Nickname == settings.ChannelNickname)
            .ToList();

        var kept = ours.FirstOrDefault(c => c.WebhookUrl == webhookUrl.AbsoluteUri);

        foreach (var stale in ours.Where(c => !ReferenceEquals(c, kept)))
        {
            await DeleteAndLogAsync(channels, stale, cancellationToken).ConfigureAwait(false);
        }

        if (kept is null)
        {
            kept = await channels.CreateWebhookChannelAsync(settings.ChannelNickname, webhookUrl, cancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Made GoTo channel {ChannelId} ({Nickname}).", kept.ChannelId, kept.Nickname);
        }

        // Read before subscribing, never after: GoTo's read lags a subscribe by a second or two.
        var accounts = await callEvents.ReadSubscribedAccountKeysAsync(kept.ChannelId, cancellationToken).ConfigureAwait(false);

        if (accounts.Contains(settings.AccountKey))
        {
            return;
        }

        logger.LogWarning("GoTo channel {ChannelId} has no call-events subscription for the account; subscribing.", kept.ChannelId);

        await callEvents.SubscribeToCallEventsAsync(kept.ChannelId, cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The GoTo channel job failed; trying again next tick.");
        }
    }

    /// <summary>One failed delete does not stop the others, or the create after them.</summary>
    private async Task DeleteAndLogAsync(
        IGoToNotificationChannelApiClient channels, GoToChannel stale, CancellationToken cancellationToken)
    {
        try
        {
            await channels.DeleteChannelAsync(stale.Nickname, stale.ChannelId, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Deleted stale GoTo channel {ChannelId} ({Nickname}).", stale.ChannelId, stale.Nickname);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
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
