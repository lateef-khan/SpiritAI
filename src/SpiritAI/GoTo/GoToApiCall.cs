using System.Net;

namespace SpiritAI.GoTo;

/// <summary>Sends one request to the GoTo API, signed, and throws with GoTo's own words when it fails.</summary>
internal static class GoToApiCall
{
    /// <summary>
    /// Signs and sends <paramref name="request"/>. A <c>401</c> drops the kept token, so the next
    /// call swaps the PAT again.
    /// </summary>
    /// <returns>The successful answer; the caller disposes it.</returns>
    /// <exception cref="HttpRequestException">GoTo answered with a status outside 2xx.</exception>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        IGoToRequestAuthorizer authorizer,
        IGoToAuthTokenProvider tokens,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);

        var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await tokens.InvalidateBearerTokenAsync(cancellationToken).ConfigureAwait(false);
            }

            var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            throw new HttpRequestException(
                $"GoTo answered {(int)response.StatusCode} to {request.Method} {request.RequestUri?.PathAndQuery}: {said}",
                inner: null,
                response.StatusCode);
        }
    }
}
