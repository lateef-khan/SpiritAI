namespace SpiritAI.GoTo;

/// <summary>
/// Work a feature does when GoTo says a call's report is ready. <see cref="GoToCallEventReader"/>
/// calls every registered handler, one event at a time, in a scope of its own.
/// </summary>
public interface IGoToCallReportHandler
{
    /// <summary>Handles one report event. A throw is logged and the next event goes on.</summary>
    /// <param name="conversationSpaceId">The call whose report is ready.</param>
    /// <param name="cancellationToken">Stops the work when the host stops.</param>
    Task HandleAsync(string conversationSpaceId, CancellationToken cancellationToken);
}
