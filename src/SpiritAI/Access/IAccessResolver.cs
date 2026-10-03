namespace SpiritAI.Access;

/// <summary>Reads one person's access.</summary>
public interface IAccessResolver
{
    /// <param name="userId">The user's id in <c>neon_auth."user"</c>, which is the token's <c>sub</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Not banned and no permissions when the user is unknown.</returns>
    ValueTask<PersonAccess> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}
