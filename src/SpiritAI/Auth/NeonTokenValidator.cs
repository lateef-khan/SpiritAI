using System.Buffers.Text;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Org.BouncyCastle.Crypto.Signers;

namespace SpiritAI.Auth;

/// <summary>The outcome of checking one bearer token.</summary>
/// <param name="Principal">Who the token says is calling, or <see langword="null"/> if it is not valid.</param>
/// <param name="Failure">Why it was refused. Never shown to the caller — a 401 says only that.</param>
public readonly record struct TokenResult(ClaimsPrincipal? Principal, string? Failure)
{
    public static TokenResult Valid(ClaimsPrincipal principal) => new(principal, null);

    public static TokenResult Invalid(string failure) => new(null, failure);
}

/// <summary>
/// Checks a Neon-issued JWT: the signature first, then the claims.
/// </summary>
public sealed class NeonTokenValidator(
    NeonSigningKeys keys,
    IOptions<NeonAuthOptions> options,
    TimeProvider clock)
{
    private readonly NeonAuthOptions _options = options.Value;

    /// <summary>Validates one compact-serialised JWT.</summary>
    /// <param name="token">The value after "Bearer ".</param>
    /// <param name="cancellationToken">Abandons a key fetch with the request.</param>
    public async Task<TokenResult> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        // header.payload.signature — three parts, and the signature covers the first two verbatim,
        // so the raw text is what gets verified rather than anything re-encoded from the parse.
        var firstDot = token.IndexOf('.');
        var lastDot = token.LastIndexOf('.');

        if (firstDot <= 0 || lastDot <= firstDot || lastDot == token.Length - 1)
        {
            return TokenResult.Invalid("the token is not three dot-separated parts.");
        }

        if (!TryDecode(token.AsSpan(0, firstDot), out var headerBytes)
            || !TryDecode(token.AsSpan(lastDot + 1), out var signature)
            || !TryDecode(token.AsSpan(firstDot + 1, lastDot - firstDot - 1), out var payloadBytes))
        {
            return TokenResult.Invalid("a token part is not base64url.");
        }

        string? keyId;
        try
        {
            using var header = JsonDocument.Parse(headerBytes);

            if (Text(header.RootElement, "alg") != "EdDSA")
            {
                // Refusing every other algorithm by name is what stops the "alg": "none" trick and
                // its relatives. Neon issues EdDSA and nothing else, so nothing legitimate is lost.
                return TokenResult.Invalid("the token is not signed with EdDSA.");
            }

            keyId = Text(header.RootElement, "kid");
        }
        catch (JsonException)
        {
            return TokenResult.Invalid("the token header is not JSON.");
        }

        if (keyId is not { Length: > 0 })
        {
            return TokenResult.Invalid("the token header names no key.");
        }

        var key = await keys.FindAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            return TokenResult.Invalid($"no published key is named {keyId}.");
        }

        var signed = Encoding.ASCII.GetBytes(token[..lastDot]);
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, key);
        verifier.BlockUpdate(signed, 0, signed.Length);

        if (!verifier.VerifySignature(signature))
        {
            return TokenResult.Invalid("the signature does not verify.");
        }

        return ReadClaims(payloadBytes);
    }

    private TokenResult ReadClaims(byte[] payloadBytes)
    {
        JsonDocument payload;
        try
        {
            payload = JsonDocument.Parse(payloadBytes);
        }
        catch (JsonException)
        {
            return TokenResult.Invalid("the token payload is not JSON.");
        }

        using (payload)
        {
            var root = payload.RootElement;
            var now = clock.GetUtcNow();

            if (Text(root, "iss") != _options.Issuer)
            {
                return TokenResult.Invalid("the token was issued by someone else.");
            }

            // Neon puts the same origin in both. Checking it stops a token minted for a different
            // service on the same issuer from being replayed here.
            if (Text(root, "aud") is { } audience && audience != _options.Issuer)
            {
                return TokenResult.Invalid("the token was issued for a different audience.");
            }

            if (Seconds(root, "exp") is not { } expires)
            {
                return TokenResult.Invalid("the token does not expire.");
            }

            if (expires + _options.ClockSkew < now)
            {
                return TokenResult.Invalid("the token has expired.");
            }

            if (Seconds(root, "nbf") is { } notBefore && notBefore - _options.ClockSkew > now)
            {
                return TokenResult.Invalid("the token is not valid yet.");
            }

            if (Text(root, "sub") is not { Length: > 0 } subject)
            {
                return TokenResult.Invalid("the token names no subject.");
            }

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };

            if (Text(root, "email") is { Length: > 0 } email)
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
                claims.Add(new Claim(ClaimTypes.Name, email));
            }

            var identity = new ClaimsIdentity(claims, NeonAuthenticationDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role);
            return TokenResult.Valid(new ClaimsPrincipal(identity));
        }
    }

    private static bool TryDecode(ReadOnlySpan<char> part, out byte[] bytes)
    {
        if (!Base64Url.IsValid(part))
        {
            bytes = [];
            return false;
        }

        bytes = Base64Url.DecodeFromChars(part);
        return true;
    }

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? Seconds(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
