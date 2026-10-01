using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>
/// The wire under <see cref="GoToClient"/>.
/// </summary>
internal sealed class GoToApi(
    HttpClient http,
    IGoToRequestAuthorizer authorizer,
    IGoToAuthTokenProvider tokens,
    IOptions<GoToOptions> options)
{
    /// <summary>A stop for a page marker that never runs out.</summary>
    public const int MaxPages = 50;

    /// <summary>The account in <see cref="GoToOptions.AccountKey"/>.</summary>
    /// <returns>The key.</returns>
    /// <exception cref="InvalidOperationException">It is not set.</exception>
    public string AccountKey()
    {
        var accountKey = options.Value.AccountKey;

        return string.IsNullOrWhiteSpace(accountKey)
            ? throw new InvalidOperationException($"{GoToOptions.SectionName}:{nameof(GoToOptions.AccountKey)} is missing.")
            : accountKey;
    }

    /// <summary>Signs and sends, as <see cref="GoToApiCall.SendAsync"/> does.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => GoToApiCall.SendAsync(http, authorizer, tokens, request, cancellationToken);

    /// <summary>Sends a GET and reads its JSON.</summary>
    public async Task<JsonElement> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Every item of a GoTo list that pages with <c>pageMarker</c>, all pages. A page without
    /// <c>items</c> reads as empty.
    /// </summary>
    public async IAsyncEnumerable<JsonElement> ItemsAsync(
        string firstPage, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? marker = null;

        for (var page = 0; page < MaxPages; page++)
        {
            var path = marker is null ? firstPage : $"{firstPage}&pageMarker={Uri.EscapeDataString(marker)}";
            var answer = await GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);

            if (answer.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    yield return item;
                }
            }

            marker = answer.TryGetProperty("nextPageMarker", out var next) ? next.GetString() : null;

            if (string.IsNullOrEmpty(marker))
            {
                yield break;
            }
        }

        throw new InvalidOperationException($"GoTo listed more than {MaxPages} pages of {firstPage}.");
    }
}
