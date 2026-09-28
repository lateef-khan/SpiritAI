namespace SpiritAI.GoTo;

/// <summary>
/// Hands out a GoTo access token, swapped from the Personal Access Token and kept until shortly
/// before it expires.
/// </summary>
public interface IGoToAuthTokenProvider
{
    /// <summary>Gives a token that is good for at least another minute.</summary>
    /// <param name="cancellationToken">Cancels the swap.</param>
    /// <returns>The bare access token, without the <c>Bearer</c> scheme.</returns>
    Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the kept token, so the next call swaps the PAT again.</summary>
    /// <param name="cancellationToken">Cancels the drop.</param>
    Task InvalidateBearerTokenAsync(CancellationToken cancellationToken = default);
}
