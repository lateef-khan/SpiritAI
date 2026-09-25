namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo call-events subscription on a notification channel, which makes GoTo post live calls
/// to it. Only a read is retried; a subscribe is sent once.
/// </summary>
public interface IGoToCallEventsApiClient
{
    /// <summary>
    /// Subscribes a channel to the <c>STARTING</c>, <c>ACTIVE</c> and <c>ENDING</c> call events of the
    /// account in <see cref="GoToOptions.AccountKey"/>. A ringing line shows only in <c>ACTIVE</c>
    /// events. A repeat subscribe is harmless, but it does not change the events of a subscription
    /// that exists, and the read does not show them.
    /// </summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task SubscribeToCallEventsAsync(string channelId, CancellationToken cancellationToken = default);

    /// <summary>Reads which accounts' call events a channel is subscribed to.</summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account keys; empty when the channel has no subscription.</returns>
    Task<IReadOnlyList<string>> ReadSubscribedAccountKeysAsync(string channelId, CancellationToken cancellationToken = default);
}
