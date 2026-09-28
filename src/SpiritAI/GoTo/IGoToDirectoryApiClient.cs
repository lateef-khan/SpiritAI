namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo calls that say who owns each phone line. The PAT needs <c>users.v1.lines.read</c> and
/// <c>identity:</c>.
/// </summary>
public interface IGoToDirectoryApiClient
{
    /// <summary>Every line in the account and the user who owns it.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>One entry per line.</returns>
    Task<IReadOnlyList<GoToLineOwner>> ListLineOwnersAsync(CancellationToken cancellationToken = default);

    /// <summary>Every user in the account and their sign-in email.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>The email by user key.</returns>
    Task<IReadOnlyDictionary<string, string>> ListUserEmailsAsync(CancellationToken cancellationToken = default);
}
