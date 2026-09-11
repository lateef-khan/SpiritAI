using SpiritAI.RealTime;

namespace SpiritAI.Tests.RealTime;

/// <summary>An <see cref="IRealTimePublisher"/> that keeps every push: the groups, the event, and what it carried.</summary>
internal sealed class RecordingRealTimePublisher : IRealTimePublisher
{
    /// <summary>Every push, in order.</summary>
    public List<(IReadOnlyList<string> Groups, string Event, object Payload)> Pushed { get; } = [];

    public ValueTask PublishAsync(string group, string eventName, object payload, CancellationToken cancellationToken)
        => PublishAsync([group], eventName, payload, cancellationToken);

    public ValueTask PublishAsync(IReadOnlyList<string> groups, string eventName, object payload, CancellationToken cancellationToken)
    {
        Pushed.Add((groups, eventName, payload));
        return ValueTask.CompletedTask;
    }
}
