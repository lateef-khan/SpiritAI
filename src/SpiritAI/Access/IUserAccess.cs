namespace SpiritAI.Access;

/// <summary>The access groups one Neon Auth user holds, through their roles.</summary>
public interface IUserAccess
{
    /// <summary>Reads the groups of one user.</summary>
    /// <param name="userId">The user's id in <c>neon_auth."user"</c>, which is the token's <c>sub</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups, empty when the user has no role that grants one.</returns>
    ValueTask<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId, CancellationToken cancellationToken = default);
}
