using SpiritAI.Threads;

namespace SpiritAI.Tests.Threads;

/// <summary>Which calls have a live session, and which were asked to get one.</summary>
internal sealed class FakeSessions : IThreadSessions
{
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);

    public List<string> Reopened { get; } = [];

    public void MarkLive(string callId) => _live.Add(callId);

    public ValueTask<bool> IsLiveAsync(string callId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_live.Contains(callId));

    public ValueTask ReopenAsync(string callId, CancellationToken cancellationToken = default)
    {
        Reopened.Add(callId);
        _live.Add(callId);
        return ValueTask.CompletedTask;
    }
}
