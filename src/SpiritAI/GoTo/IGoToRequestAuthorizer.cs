namespace SpiritAI.GoTo;

/// <summary>Signs a request to the GoTo API with the current access token.</summary>
public interface IGoToRequestAuthorizer
{
    /// <summary>Puts <c>Authorization: Bearer &lt;token&gt;</c> on the request.</summary>
    /// <param name="request">The request to sign.</param>
    /// <param name="cancellationToken">Cancels a token swap, when one is needed.</param>
    Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
