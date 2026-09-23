using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Checks the signature Chatwoot puts on a webhook: <c>X-Chatwoot-Signature</c> is
/// <c>sha256=</c> and the hex HMAC-SHA256, keyed by the inbox secret, of
/// <c>"{X-Chatwoot-Timestamp}.{body}"</c> (<c>Webhooks::Trigger#request_headers</c>).
/// </summary>
public static class ChatwootSignature
{
    /// <summary>The header that carries the signature.</summary>
    public const string SignatureHeader = "X-Chatwoot-Signature";

    /// <summary>The header that carries the Unix time the signature was made at.</summary>
    public const string TimestampHeader = "X-Chatwoot-Timestamp";

    private const string Prefix = "sha256=";

    /// <summary>Whether a webhook is Chatwoot's, and recent.</summary>
    /// <param name="secret">The inbox secret.</param>
    /// <param name="timestamp">The timestamp header as sent.</param>
    /// <param name="signature">The signature header as sent.</param>
    /// <param name="body">The raw body, byte for byte.</param>
    /// <param name="now">The time now.</param>
    /// <param name="maxSkew">How far the timestamp may be from <paramref name="now"/>.</param>
    /// <returns><see langword="true"/> when the signature matches and the timestamp is within the skew.</returns>
    public static bool IsValid(string secret, string? timestamp, string? signature, ReadOnlySpan<byte> body, DateTimeOffset now, TimeSpan maxSkew)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        if (string.IsNullOrEmpty(timestamp)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || string.IsNullOrEmpty(signature)
            || !signature.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if ((now - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > maxSkew)
        {
            return false;
        }

        byte[] sent;
        
        try
        {
            sent = Convert.FromHexString(signature.AsSpan(Prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var signed = new byte[timestamp.Length + 1 + body.Length];
        Encoding.ASCII.GetBytes(timestamp, signed);
        signed[timestamp.Length] = (byte)'.';
        body.CopyTo(signed.AsSpan(timestamp.Length + 1));

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);

        return CryptographicOperations.FixedTimeEquals(expected, sent);
    }
}
