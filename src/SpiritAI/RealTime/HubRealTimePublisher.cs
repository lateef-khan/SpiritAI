using Microsoft.AspNetCore.SignalR;

namespace SpiritAI.RealTime;

/// <summary>The <see cref="IRealTimePublisher"/> over <see cref="SpiritHub"/>.</summary>
internal sealed class HubRealTimePublisher(IHubContext<SpiritHub> hub) : IRealTimePublisher
{
    /// <inheritdoc />
    public async ValueTask PublishAsync(string group, string eventName, object payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        await hub.Clients.Group(group).SendAsync(eventName, payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync(IReadOnlyList<string> groups, string eventName, object payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentException.ThrowIfNullOrEmpty(eventName);

        await hub.Clients.Groups(groups).SendAsync(eventName, payload, cancellationToken).ConfigureAwait(false);
    }
}
