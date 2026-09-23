using System.Text;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// The signature check against a value Chatwoot's own Ruby made: <c>OpenSSL::HMAC.hexdigest</c>
/// run inside the <c>chatwoot/chatwoot:v4.18.0-ce</c> image, the way <c>Webhooks::Trigger</c> signs.
/// </summary>
public sealed class ChatwootSignatureTests
{
    private const string Secret = "test-secret";
    private const string Timestamp = "1790000000";
    private const string Body = "{\"event\":\"message_created\",\"id\":7,\"content\":\"Hi — ünïcode\"}";
    private const string RubySignature = "sha256=5679197aeae748a235f496b103d97ddf6b8d5024f8dc57476eecc61439a2c1cc";

    private static readonly DateTimeOffset SignedAt = DateTimeOffset.FromUnixTimeSeconds(1790000000);
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(5);

    [Fact]
    public void ChatwootsOwnSignatureIsAccepted()
        => Assert.True(ChatwootSignature.IsValid(Secret, Timestamp, RubySignature, Encoding.UTF8.GetBytes(Body), SignedAt, Skew));

    [Fact]
    public void AChangedBodyIsRefused()
        => Assert.False(ChatwootSignature.IsValid(Secret, Timestamp, RubySignature, Encoding.UTF8.GetBytes(Body + " "), SignedAt, Skew));

    [Fact]
    public void AnotherSecretIsRefused()
        => Assert.False(ChatwootSignature.IsValid("other-secret", Timestamp, RubySignature, Encoding.UTF8.GetBytes(Body), SignedAt, Skew));

    [Fact]
    public void AnOldSignatureIsRefused()
        => Assert.False(ChatwootSignature.IsValid(Secret, Timestamp, RubySignature, Encoding.UTF8.GetBytes(Body), SignedAt.AddMinutes(6), Skew));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("5679197aeae748a235f496b103d97ddf6b8d5024f8dc57476eecc61439a2c1cc")]
    [InlineData("sha256=not-hex")]
    public void AMalformedSignatureIsRefused(string? signature)
        => Assert.False(ChatwootSignature.IsValid(Secret, Timestamp, signature, Encoding.UTF8.GetBytes(Body), SignedAt, Skew));
}
