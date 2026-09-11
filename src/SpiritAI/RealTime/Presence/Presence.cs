namespace SpiritAI.RealTime.Presence;

/// <summary>
/// One open socket: <c>spirit.presence</c>.
/// </summary>
public sealed class Presence
{
    /// <summary>The socket's own id.</summary>
    public required string ConnectionId { get; set; }

    /// <summary>The caller key of whoever is on the socket.</summary>
    public required string CallerKey { get; set; }

    /// <summary>Their display name, when they have one.</summary>
    public string? CallerName { get; set; }

    /// <summary>Which kind of caller, as the admission that let them in named it.</summary>
    public required string Kind { get; set; }

    /// <summary>When the socket opened. The database fills it in when left unset.</summary>
    public DateTimeOffset ConnectedAt { get; set; }

    /// <summary>When the socket was last touched. The database fills it in when left unset.</summary>
    public DateTimeOffset SeenAt { get; set; }
}
