namespace SpiritAI.Contacts;

/// <summary>
/// One key that names a person: <c>spirit.contact_identity</c>.
/// </summary>
public sealed class ContactIdentity
{
    /// <summary>What kind of key this is.</summary>
    public ContactIdentityKind Kind { get; set; }

    /// <summary>The key itself, normalised per <see cref="Kind"/>.</summary>
    public required string Value { get; set; }

    /// <summary>The person this key names.</summary>
    public long ContactId { get; set; }

    /// <summary>
    /// Proved (caller ID), or only typed. Decision 5.3: only a verified key joins automatically.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>When this key was first seen. The database fills it in when left unset.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }
}
