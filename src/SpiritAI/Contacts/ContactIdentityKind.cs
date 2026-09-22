namespace SpiritAI.Contacts;

/// <summary>
/// What a <see cref="ContactIdentity"/> proves. Stored as lowercase text: <c>visitor</c>,
/// <c>phone</c>, <c>email</c>.
/// </summary>
public enum ContactIdentityKind
{
    /// <summary>The random key a browser keeps in <c>localStorage</c>. Proof of itself.</summary>
    Visitor,

    /// <summary>A phone number. Verified only when it comes from caller ID.</summary>
    Phone,

    /// <summary>An email address. Verified by nobody until it is proved another way.</summary>
    Email,
}
