namespace SpiritAI.GoTo;

/// <summary>
/// Work a feature does on a call that has staff lines in it. <see cref="GoToCallEventReader"/>
/// calls every registered handler, one event at a time, in a scope of its own.
/// </summary>
public interface IGoToCallHandler
{
    /// <summary>Handles one event of a call. A throw is logged and the next event goes on.</summary>
    /// <param name="call">The call, as the event shows it.</param>
    /// <param name="cancellationToken">Stops the work when the host stops.</param>
    Task HandleAsync(GoToCall call, CancellationToken cancellationToken);
}
