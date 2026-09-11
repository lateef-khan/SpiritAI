namespace SpiritAI.Handoffs.Model;

/// <summary>
/// One open staff socket: <c>spirit.staff_presence</c>.
/// </summary>
public sealed class StaffPresence
{
    /// <summary>The socket's own id.</summary>
    public required string ConnectionId { get; set; }

    /// <summary>The caller key of the member of staff on the socket.</summary>
    public required string StaffKey { get; set; }

    /// <summary>Their name.</summary>
    public required string StaffName { get; set; }

    /// <summary>When the socket opened. The database fills it in when left unset.</summary>
    public DateTimeOffset ConnectedAt { get; set; }

    /// <summary>When the socket was last touched. The database fills it in when left unset.</summary>
    public DateTimeOffset SeenAt { get; set; }
}
