namespace SpiritAI.Access;

/// <summary>A ban: while the row exists, Spirit refuses the person on every <c>/v1</c> route but <c>/v1/me</c>.</summary>
public sealed class PersonBan
{
    public Guid UserId { get; set; }

    public DateTimeOffset BannedAt { get; set; }

    /// <summary>Who banned them; <see langword="null"/> for a ban copied from Neon.</summary>
    public Guid? BannedBy { get; set; }

    public string? Reason { get; set; }
}
