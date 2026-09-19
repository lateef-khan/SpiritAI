using SpiritAI.Threads;

namespace SpiritAI.Tests.Threads;

/// <summary>Which conversations have a live session, and which were asked to get one.</summary>
internal sealed class FakeSessions : IThreadSessions
{
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);

    public List<string> Reopened { get; } = [];

    public void MarkLive(string conversationId) => _live.Add(conversationId);

    public ValueTask<bool> IsLiveAsync(string conversationId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_live.Contains(conversationId));

    public ValueTask ReopenAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        Reopened.Add(conversationId);
        _live.Add(conversationId);
        return ValueTask.CompletedTask;
    }
}
