namespace SpiritAI.RealTime;

/// <summary>
/// How the hub is run, bound from the <see cref="SectionName"/> section.
/// </summary>
public sealed class RealTimeOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "RealTime";

    /// <summary>
    /// The Redis connection string of the backplane that lets two Machines share one hub.
    /// <see langword="null"/> runs the hub on one Machine alone, which is what development does.
    /// A secret: on Fly it comes from <c>fly secrets set RealTime__Redis=…</c>.
    /// </summary>
    public string? Redis { get; set; }

    /// <summary>
    /// How often, in seconds, a client calls <c>Heartbeat</c> and the sweeper looks for sockets
    /// that never said goodbye.
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 30;

    /// <summary>
    /// How long, in seconds, a socket counts as online after its last heartbeat. Three
    /// heartbeats, so one dropped packet does not sign anyone out.
    /// </summary>
    public int PresenceWindowSeconds { get; set; } = 90;
}
