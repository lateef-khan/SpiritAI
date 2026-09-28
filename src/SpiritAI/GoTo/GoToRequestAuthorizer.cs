using System.Net.Http.Headers;

namespace SpiritAI.GoTo;

/// <summary>Signs each GoTo API request with the token the provider holds.</summary>
public sealed class GoToRequestAuthorizer(IGoToAuthTokenProvider tokens) : IGoToRequestAuthorizer
{
    /// <inheritdoc />
    public async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await tokens.GetBearerTokenAsync(cancellationToken).ConfigureAwait(false);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
