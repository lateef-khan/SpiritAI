namespace SpiritAI.Access;

/// <summary>
/// The one column of Neon Auth's <c>neon_auth."user"</c> that the <see cref="UserRole"/> foreign key
/// points at.
/// </summary>
public sealed class NeonUserStub
{
    /// <summary>The primary key of <c>neon_auth."user"</c>.</summary>
    public Guid Id { get; set; }
}
