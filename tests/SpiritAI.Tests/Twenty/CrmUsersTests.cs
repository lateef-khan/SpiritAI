using System.Net;

using Microsoft.Extensions.Options;

using SpiritAI.Hub;
using SpiritAI.Tests.Auth;
using SpiritAI.Twenty;

using Xunit;

namespace SpiritAI.Tests.Twenty;

public sealed class CrmUsersTests
{
    [Fact]
    public async Task ANewUser_IsAskedOfTheFork_WithTheSecretAndASplitName()
    {
        var wire = new ReplayingHandler("crm_user_created", HttpStatusCode.Created) { Folder = "Hub" };

        var id = await Users(wire).CreateUserAsync("JW Hackett", "jw.hackett@spiritfitness.test", Cancel);

        Assert.Equal("b3b3aae7-1714-4fd6-9002-093eab25e3bd", id);
        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://twenty.test/auth/spirit/users", request.Url);
        Assert.Equal("Bearer hub-secret", request.Authorization);
        Assert.Equal("""{"email":"jw.hackett@spiritfitness.test","firstName":"JW","lastName":"Hackett"}""", request.Body);
    }

    [Fact]
    public void TheSignInUrl_GoesToTheForkOnTheCrmAddress_WithANote()
    {
        var url = new Uri(Users(new ReplayingHandler(payload: null)).SignInUrl("3b7c1d52"));

        Assert.Equal("https://crm.spirit.test/auth/spirit", url.GetLeftPart(UriPartial.Path));
        Assert.StartsWith("note=ey", url.Query.TrimStart('?'));
    }

    [Fact]
    public async Task AnEmptyBaseUrl_IsCrmUnavailable_NotARelativeUriCrash()
    {
        var options = Options.Create(new TwentyOptions { BaseUrl = string.Empty, HubSecret = "hub-secret" });
        var users = new CrmUsers(
            new HttpClient(new ReplayingHandler(payload: null)),
            options,
            Options.Create(new HubOptions { CrmUrl = "https://crm.spirit.test" }),
            new TestTimeProvider(DateTimeOffset.UtcNow));

        var refused = await Assert.ThrowsAsync<CrmUnavailableException>(
            () => users.CreateUserAsync("Ann Lee", "ann.lee@spiritfitness.test", Cancel));
        Assert.Equal("CRM is not set up yet.", refused.Message);
    }

    [Fact]
    public async Task AFailedCreate_IsCrmUnavailable()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.NotFound) { Folder = "Hub" };

        var refused = await Assert.ThrowsAsync<CrmUnavailableException>(
            () => Users(wire).CreateUserAsync("Ann Lee", "ann.lee@spiritfitness.test", Cancel));
        Assert.Equal("CRM is not set up yet.", refused.Message);
    }

    [Fact]
    public async Task AOneWordName_SplitsToFirstNameOnly()
    {
        var wire = new ReplayingHandler("crm_user_created", HttpStatusCode.Created) { Folder = "Hub" };

        var id = await Users(wire).CreateUserAsync("Cher", "cher@spiritfitness.test", Cancel);

        Assert.Equal("b3b3aae7-1714-4fd6-9002-093eab25e3bd", id);
        var request = Assert.Single(wire.Requests);
        Assert.Equal("""{"email":"cher@spiritfitness.test","firstName":"Cher","lastName":""}""", request.Body);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static CrmUsers Users(ReplayingHandler wire) => new(
        new HttpClient(wire),
        Options.Create(new TwentyOptions { BaseUrl = "http://twenty.test", HubSecret = "hub-secret" }),
        Options.Create(new HubOptions { CrmUrl = "https://crm.spirit.test" }),
        new TestTimeProvider(DateTimeOffset.UtcNow));
}
