namespace SpiritAI.Tests;

/// <summary>Fires every timer at once, so a retry's wait does not wait, and keeps each wait it was asked for.</summary>
internal sealed class NoWaitClock : TimeProvider
{
    private readonly object gate = new();

    private readonly List<TimeSpan> delays = [];

    /// <summary>The wait of every timer created so far, in order.</summary>
    public IReadOnlyList<TimeSpan> Delays
    {
        get
        {
            lock (gate)
            {
                return [.. delays];
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            delays.Add(dueTime);
        }

        return base.CreateTimer(callback, state, dueTime == Timeout.InfiniteTimeSpan ? dueTime : TimeSpan.Zero, period);
    }
}
