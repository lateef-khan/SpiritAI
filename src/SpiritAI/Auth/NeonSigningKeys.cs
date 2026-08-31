using System.Buffers.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Org.BouncyCastle.Crypto.Parameters;

namespace SpiritAI.Auth;

/// <summary>
/// Neon's public signing keys, fetched once and kept.
/// </summary>
public sealed class NeonSigningKeys(
    HttpClient http,
    IOptions<NeonAuthOptions> options,
    TimeProvider clock,
    ILogger<NeonSigningKeys> logger)
{
    private readonly NeonAuthOptions _options = options.Value;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private Dictionary<string, Ed25519PublicKeyParameters> _keys = [];

    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Finds the key a token names, fetching the set if this is the first ask or if the key is one
    /// we have not seen and the cooldown has passed.
    /// </summary>
    /// <param name="keyId">The token header's <c>kid</c>.</param>
    /// <param name="cancellationToken">Abandons the fetch with the request.</param>
    /// <returns>The public key, or <see langword="null"/> if Neon does not publish one by that name.</returns>
    public async Task<Ed25519PublicKeyParameters?> FindAsync(string keyId, CancellationToken cancellationToken)
    {
        if (_keys.TryGetValue(keyId, out var known))
        {
            return known;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Another request may have fetched while this one waited for the gate.
            if (_keys.TryGetValue(keyId, out var justFetched))
            {
                return justFetched;
            }

            if (clock.GetUtcNow() - _fetchedAt < _options.KeyRefreshCooldown)
            {
                return null;
            }

            _keys = await FetchAsync(cancellationToken).ConfigureAwait(false);
            _fetchedAt = clock.GetUtcNow();

            return _keys.GetValueOrDefault(keyId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, Ed25519PublicKeyParameters>> FetchAsync(CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Fetching Neon signing keys from {JwksUri}.", _options.JwksUri);

        using var response = await http.GetAsync(_options.JwksUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);

        var keys = new Dictionary<string, Ed25519PublicKeyParameters>(StringComparer.Ordinal);

        if (!document.RootElement.TryGetProperty("keys", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("The Neon key set carried no \"keys\" array.");
            return keys;
        }

        foreach (var key in array.EnumerateArray())
        {
            // Neon publishes only Ed25519 today, but a key set is allowed to carry anything. A
            // shape we do not understand is skipped, not treated as a failure of the whole fetch.
            if (Text(key, "kty") != "OKP" || Text(key, "crv") != "Ed25519")
            {
                continue;
            }

            if (Text(key, "kid") is not { Length: > 0 } kid || Text(key, "x") is not { Length: > 0 } x)
            {
                continue;
            }

            if (Base64Url.IsValid(x))
            {
                keys[kid] = new Ed25519PublicKeyParameters(Base64Url.DecodeFromChars(x));
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Neon published {Count} Ed25519 signing key(s).", keys.Count);

        return keys;
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
