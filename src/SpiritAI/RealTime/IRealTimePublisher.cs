namespace SpiritAI.RealTime;

/// <summary>
/// Pushes an event to the sockets of a group. Features call this after a state change has
/// committed; what carries it to a browser is behind this port.
/// </summary>
public interface IRealTimePublisher
{
    /// <summary>Pushes to one group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="eventName">What the browser subscribes to.</param>
    /// <param name="payload">What the event carries, serialised as JSON.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask PublishAsync(string group, string eventName, object payload, CancellationToken cancellationToken);

    /// <summary>Pushes to several groups at once. A socket in more than one of them hears it once.</summary>
    /// <param name="groups">The groups.</param>
    /// <param name="eventName">What the browser subscribes to.</param>
    /// <param name="payload">What the event carries, serialised as JSON.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    ValueTask PublishAsync(IReadOnlyList<string> groups, string eventName, object payload, CancellationToken cancellationToken);
}
