using SpiritAI.Access;

namespace SpiritAI.Settings;

/// <summary>How <see cref="PersonAdding.AddAsync"/> ended: refused before anything was made, or made.</summary>
public sealed record AddOutcome(AddedPerson? Added, AccessWrite? Refused, bool EmailTaken, bool Banned)
{
    public static AddOutcome Taken { get; } = new(null, null, true, false);

    /// <summary>The email's sign-in exists, has no roles, and is banned.</summary>
    public static AddOutcome BannedPerson { get; } = new(null, null, false, true);

    public static AddOutcome Made(AddedPerson added) => new(added, null, false, false);

    public static AddOutcome Refuse(AccessWrite refused) => new(null, refused, false, false);
}
