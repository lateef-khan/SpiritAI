namespace SpiritAI.Auth.Users;

/// <summary>Who has a Neon sign-in. The port a feature asks about a person.</summary>
public interface IUserDirectory
{
    /// <summary>Finds one person by the address on their sign-in.</summary>
    /// <param name="email">The address. Matched without regard to case.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The person, or <see langword="null"/> when nobody has that address.</returns>
    ValueTask<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
