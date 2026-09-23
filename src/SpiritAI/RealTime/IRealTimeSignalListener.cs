namespace SpiritAI.RealTime;

/// <summary>
/// Hears every signal the hub relays, after the caller was allowed to send it. A feature uses it
/// to pass a hint on to somebody who is not on the socket.
/// </summary>
/// <remarks>
/// The hub calls it inside the sender's invocation, so it must return at once: queue the work and
/// do it elsewhere. A signal is a hint, and a listener may drop it.
/// </remarks>
public interface IRealTimeSignalListener
{
    /// <summary>Takes one relayed signal.</summary>
    /// <param name="caller">Who sent it, as their admission described them.</param>
    /// <param name="signal">The signal, as the group received it.</param>
    void Heard(RealTimeCaller caller, RealTimeSignal signal);
}
