using System.Buffers.Text;
using System.Text.Json;

using SpiritAI.Hub;
using SpiritAI.Tests.Auth;

using Xunit;

namespace SpiritAI.Tests.Hub;

public sealed class HubNoteTests
{
    [Fact]
    public void SigningTheRfc7515Example_GivesTheRfcSignature()
    {
        // RFC 7515, appendix A.1: the exact header and payload octets and the JWK "k" value.
        const string header = "{\"typ\":\"JWT\",\r\n \"alg\":\"HS256\"}";
        const string payload = "{\"iss\":\"joe\",\r\n \"exp\":1300819380,\r\n \"http://example.com/is_root\":true}";
        var key = Base64Url.DecodeFromChars(
            "AyM1SysPpbyDfgZld3umj1qzKObwVMkoqQ-EstJQLr_T-1qS0gZH75aKtMN3Yj0iPS4hcgUuTwjAzZr1Z9CAow");

        var token = HubNote.Sign(header, payload, key);

        Assert.Equal(
            "eyJ0eXAiOiJKV1QiLA0KICJhbGciOiJIUzI1NiJ9" +
            ".eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ" +
            ".dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk",
            token);
    }

    [Fact]
    public void ACrmNote_NamesTheUser_TheCrmAudience_AndLastsSixtySeconds()
    {
        var clock = new TestTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

        var token = HubNote.ForCrm("0f5c2a8e-user", "shared-secret", clock);

        var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(token.Split('.')[1])).RootElement;
        Assert.Equal("0f5c2a8e-user", claims.GetProperty("sub").GetString());
        Assert.Equal("crm", claims.GetProperty("aud").GetString());
        Assert.Equal(1_800_000_060, claims.GetProperty("exp").GetInt64());
        Assert.Equal(32, claims.GetProperty("jti").GetString()!.Length);
    }
}
