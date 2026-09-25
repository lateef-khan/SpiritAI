namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo calls that make Spirit hear about live calls: a webhook channel, and a call-events
/// subscription on it. Each call is sent once and never retried, so a failure can never leave a
/// second channel behind.
/// </summary>
public interface IGoToCallEventsApiClient
{
    /// <summary>
    /// Makes a webhook channel. GoTo first sends <c>OPTIONS</c> to <paramref name="webhookUrl"/>,
    /// and refuses the channel unless the URL answers 2xx.
    /// </summary>
    /// <param name="nickname">Spirit's name for the channel.</param>
    /// <param name="webhookUrl">The public URL GoTo posts events to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The channel GoTo made.</returns>
    Task<GoToChannel> CreateWebhookChannelAsync(string nickname, Uri webhookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes a channel to the <c>STARTING</c> and <c>ENDING</c> call events of the account in
    /// <see cref="GoToOptions.AccountKey"/>.
    /// </summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task SubscribeToCallEventsAsync(string channelId, CancellationToken cancellationToken = default);
}
