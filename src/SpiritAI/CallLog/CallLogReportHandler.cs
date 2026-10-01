using SpiritAI.GoTo;

namespace SpiritAI.CallLog;

/// <summary>Queues each call whose report GoTo says is ready.</summary>
public sealed class CallLogReportHandler(CallLogQueue queue) : IGoToCallReportHandler
{
    /// <inheritdoc />
    public Task HandleAsync(string conversationSpaceId, CancellationToken cancellationToken)
    {
        queue.AddLive(conversationSpaceId);
        return Task.CompletedTask;
    }
}
