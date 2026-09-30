using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SpiritAI.Hub;

/// <summary>
/// The short note Spirit hands the CRM frame: an HS256 JWT the Twenty fork checks with the same
/// shared secret.
/// </summary>
public static class HubNote
{
    /// <summary>How long a note is good for.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    /// <summary>Signs a note for one Twenty user.</summary>
    /// <param name="twentyUserId">The Linked user's id in Twenty.</param>
    /// <param name="secret">The secret the fork shares.</param>
    /// <param name="clock">The clock the expiry is read from.</param>
    /// <returns>The compact JWT.</returns>
    public static string ForCrm(string twentyUserId, string secret, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrEmpty(twentyUserId);
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentNullException.ThrowIfNull(clock);

        var payload = JsonSerializer.Serialize(new
        {
            sub = twentyUserId,
            aud = "crm",
            exp = clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds(),
            jti = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)),
        });

        return Sign("""{"alg":"HS256","typ":"JWT"}""", payload, Encoding.UTF8.GetBytes(secret));
    }

    /// <summary>Signs exactly these header and payload octets.</summary>
    public static string Sign(string headerJson, string payloadJson, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(headerJson);
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentNullException.ThrowIfNull(key);

        var signingInput = $"{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(headerJson))}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payloadJson))}";
        var signature = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(signingInput));

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }
}
