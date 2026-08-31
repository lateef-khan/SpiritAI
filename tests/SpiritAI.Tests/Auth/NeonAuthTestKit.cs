using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

using SpiritAI.Auth;

using Xunit;

namespace SpiritAI.Tests.Auth;

/// <summary>
/// A Neon that only exists in the test: a real Ed25519 key pair, a real key set served over a
/// stubbed handler, and a token minted the way Neon mints them.
/// </summary>
/// <remarks>
/// Signing for real, rather than stubbing the signature check, is the point. The thing most worth
/// knowing about this code is that it rejects a token whose signature does not verify, and a fake
/// signer cannot show that.
/// </remarks>
internal sealed class NeonAuthTestKit
{
    public const string BaseUrl = "https://ep-test.neonauth.us-east-2.aws.neon.tech/neondb/auth";
    public const string Issuer = "https://ep-test.neonauth.us-east-2.aws.neon.tech";
    public const string KeyId = "test-key-1";

    private readonly Ed25519PrivateKeyParameters _private;
    private readonly Ed25519PublicKeyParameters _public;

    public NeonAuthTestKit()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));

        var pair = generator.GenerateKeyPair();
        _private = (Ed25519PrivateKeyParameters)pair.Private;
        _public = (Ed25519PublicKeyParameters)pair.Public;
    }

    /// <summary>How many times the key set has been fetched. The cooldown test reads this.</summary>
    public int JwksFetches { get; private set; }

    public TestTimeProvider Clock { get; } = new(DateTimeOffset.Parse("2026-08-31T12:00:00Z", null));

    public NeonAuthOptions Options { get; } = new() { BaseUrl = BaseUrl };

    public NeonTokenValidator Validator() => new(SigningKeys(), Microsoft.Extensions.Options.Options.Create(Options), Clock);

    public NeonSigningKeys SigningKeys()
        => new(
            new HttpClient(new JwksHandler(this)),
            Microsoft.Extensions.Options.Options.Create(Options),
            Clock,
            NullLogger<NeonSigningKeys>.Instance);

    /// <summary>Mints a token, signed for real, with whatever claims the caller wants changed.</summary>
    public string Token(
        string? issuer = Issuer,
        string? audience = Issuer,
        string? subject = "user_123",
        string? email = "someone@example.com",
        DateTimeOffset? expires = null,
        DateTimeOffset? notBefore = null,
        string algorithm = "EdDSA",
        string keyId = KeyId,
        bool corruptSignature = false)
    {
        var header = new Dictionary<string, object?> { ["alg"] = algorithm, ["typ"] = "JWT", ["kid"] = keyId };

        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["sub"] = subject,
            ["email"] = email,
            ["exp"] = (expires ?? Clock.GetUtcNow().AddMinutes(15)).ToUnixTimeSeconds(),
        };

        if (notBefore is { } nbf)
        {
            payload["nbf"] = nbf.ToUnixTimeSeconds();
        }

        Strip(header);
        Strip(payload);

        var signingInput = $"{Encode(header)}.{Encode(payload)}";

        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, _private);
        var bytes = Encoding.ASCII.GetBytes(signingInput);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        var signature = signer.GenerateSignature();

        if (corruptSignature)
        {
            signature[0] ^= 0xFF;
        }

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private string Jwks() => JsonSerializer.Serialize(new
    {
        keys = new[]
        {
            new
            {
                kty = "OKP",
                crv = "Ed25519",
                kid = KeyId,
                x = Base64Url.EncodeToString(_public.GetEncoded()),
            },
        },
    });

    private static void Strip(Dictionary<string, object?> map)
    {
        foreach (var key in map.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            map.Remove(key);
        }
    }

    private static string Encode(Dictionary<string, object?> map)
        => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(map));

    private sealed class JwksHandler(NeonAuthTestKit kit) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(kit.Options.JwksUri, request.RequestUri);
            kit.JwksFetches++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(kit.Jwks(), Encoding.UTF8, "application/json"),
            });
        }
    }
}

/// <summary>A clock the test moves by hand.</summary>
internal sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
