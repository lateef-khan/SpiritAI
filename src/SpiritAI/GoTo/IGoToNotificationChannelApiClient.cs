namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo notification channels of the PAT owner: the webhook addresses GoTo posts events to.
/// Only a list is retried; a create or delete is sent once.
/// </summary>
public interface IGoToNotificationChannelApiClient
{
    /// <summary>
    /// Makes a webhook channel, or gives back the one that has this nickname and URL already.
    /// GoTo first sends <c>OPTIONS</c> to <paramref name="webhookUrl"/>, and refuses the channel
    /// unless the URL answers 2xx.
    /// </summary>
    /// <param name="nickname">Spirit's name for the channel.</param>
    /// <param name="webhookUrl">The public URL GoTo posts events to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The channel GoTo made or found.</returns>
    Task<GoToChannel> CreateWebhookChannelAsync(string nickname, Uri webhookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every channel of the PAT owner, all pages. GoTo cannot filter by nickname, so this is
    /// every environment's channels, and other apps' too.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The channels, newest first.</returns>
    Task<IReadOnlyList<GoToChannel>> ListChannelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes a channel and its subscriptions. A channel that is gone already counts as deleted.</summary>
    /// <param name="nickname">The channel's <see cref="GoToChannel.Nickname"/>.</param>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task DeleteChannelAsync(string nickname, string channelId, CancellationToken cancellationToken = default);
}
