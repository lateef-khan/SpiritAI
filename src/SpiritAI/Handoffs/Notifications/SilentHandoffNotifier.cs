using SpiritAI.Handoffs.Contracts;

namespace SpiritAI.Handoffs.Notifications;

/// <summary>
/// The <see cref="IHandoffNotifier"/> a host runs with until it has a hub: every push goes nowhere.
/// REST is the truth and the socket a hint, so nothing is lost but the hint.
/// </summary>
internal sealed class SilentHandoffNotifier : IHandoffNotifier
{
    /// <inheritdoc />
    public ValueTask WaitingAsync(HandoffSummary handoff, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask QueueAsync(string callId, int position, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ClaimedAsync(string callId, HandoffAssignee assignee, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DoneAsync(string callId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask MessageCreatedAsync(HandoffMessage message, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
