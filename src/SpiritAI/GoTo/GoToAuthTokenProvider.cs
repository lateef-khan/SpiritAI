using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>
/// Swaps the GoTo Personal Access Token for a one-hour access token and keeps it in this server's
/// memory.
/// </summary>
public sealed class GoToAuthTokenProvider(
    HttpClient http,
    HybridCache cache,
    TimeProvider clock,
    IOptions<GoToOptions> options)
    : IGoToAuthTokenProvider
{
    /// <summary>The token host. The swap is <c>POST oauth/token</c> on it.</summary>
    public static readonly Uri TokenHost = new("https://authentication.logmeininc.com/");

    private const string CacheKey = "goto:access-token";

    /// <summary>A token this close to its end is swapped again before it is used.</summary>
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The token is a secret, so it never leaves this server for the shared second level. GoTo
    /// tokens last an hour; the entry goes a little sooner.
    /// </summary>
    private static readonly HybridCacheEntryOptions MemoryOnly = new()
    {
        Expiration = TimeSpan.FromMinutes(55),
        LocalCacheExpiration = TimeSpan.FromMinutes(55),
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    private GoToOptions Settings => options.Value;

    /// <inheritdoc />
    public async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await ReadOrSwapAsync(cancellationToken).ConfigureAwait(false);

        if (token.ExpiresAt > clock.GetUtcNow() + ExpiryBuffer)
        {
            return token.Value;
        }

        await InvalidateBearerTokenAsync(cancellationToken).ConfigureAwait(false);

        return (await ReadOrSwapAsync(cancellationToken).ConfigureAwait(false)).Value;
    }

    /// <inheritdoc />
    public async Task InvalidateBearerTokenAsync(CancellationToken cancellationToken = default)
        => await cache.RemoveAsync(CacheKey, cancellationToken).ConfigureAwait(false);

    private ValueTask<GoToAccessToken> ReadOrSwapAsync(CancellationToken cancellationToken)
        => cache.GetOrCreateAsync(CacheKey, SwapAsync, MemoryOnly, cancellationToken: cancellationToken);

    private async ValueTask<GoToAccessToken> SwapAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token")
        {
            Content = new FormUrlEncodedContent(
            [
                new("grant_type", "personal_access_token"),
                new("pat", Settings.PersonalAccessToken),
            ]),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Settings.ClientId}:{Settings.ClientSecret}")));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            throw new HttpRequestException(
                $"GoTo token host answered {(int)response.StatusCode} to the PAT swap: {said}",
                inner: null,
                response.StatusCode);
        }

        var answer = await response.Content.ReadFromJsonAsync<TokenAnswer>(cancellationToken).ConfigureAwait(false);

        if (answer?.AccessToken is not { Length: > 0 } accessToken)
        {
            throw new InvalidOperationException("GoTo token host answered without an access_token.");
        }

        return new GoToAccessToken(accessToken, clock.GetUtcNow().AddSeconds(answer.ExpiresIn ?? 3600));
    }

    private void EnsureConfigured()
    {
        Require(Settings.ClientId, nameof(GoToOptions.ClientId));
        Require(Settings.ClientSecret, nameof(GoToOptions.ClientSecret));
        Require(Settings.PersonalAccessToken, nameof(GoToOptions.PersonalAccessToken));

        static void Require(string value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{GoToOptions.SectionName}:{key} is missing.");
            }
        }
    }

    /// <summary>A kept access token and the moment GoTo stops taking it.</summary>
    private sealed record GoToAccessToken(string Value, DateTimeOffset ExpiresAt);

    private sealed record TokenAnswer(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);
}
