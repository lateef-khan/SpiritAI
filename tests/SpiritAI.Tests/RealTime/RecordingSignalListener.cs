using System.Collections.Concurrent;

using SpiritAI.RealTime;

namespace SpiritAI.Tests.RealTime;

/// <summary>An <see cref="IRealTimeSignalListener"/> that keeps what it heard.</summary>
internal sealed class RecordingSignalListener : IRealTimeSignalListener
{
    public ConcurrentQueue<(RealTimeCaller Caller, RealTimeSignal Signal)> Heard { get; } = new();

    void IRealTimeSignalListener.Heard(RealTimeCaller caller, RealTimeSignal signal) => Heard.Enqueue((caller, signal));
}
