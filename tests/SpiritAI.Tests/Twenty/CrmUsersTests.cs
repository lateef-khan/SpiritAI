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
    public async Task DeletingAUser_AsksTheFork_WithTheSecret()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.NoContent) { Folder = "Hub" };

        await Users(wire).DeleteUserAsync("b3b3aae7-1714-4fd6-9002-093eab25e3bd", Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("DELETE", request.Method);
        Assert.Equal("http://twenty.test/auth/spirit/users/b3b3aae7-1714-4fd6-9002-093eab25e3bd", request.Url);
        Assert.Equal("Bearer hub-secret", request.Authorization);
    }

    [Fact]
    public async Task TheLastAdmin_IsCrmRefused()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.Conflict) { Folder = "Hub" };

        var refused = await Assert.ThrowsAsync<CrmRefusedException>(() => Users(wire).DeleteUserAsync("u1", Cancel));
        Assert.Equal("CRM will not remove its last admin. Make another CRM admin first.", refused.Message);
    }

    [Fact]
    public async Task AFailedDelete_IsCrmUnavailable()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.BadGateway) { Folder = "Hub" };

        await Assert.ThrowsAsync<CrmUnavailableException>(() => Users(wire).DeleteUserAsync("u1", Cancel));
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
        Assert.Equal("CRM did not answer.", refused.Message);
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

    [Fact]
    public async Task ASuccessWithNoJsonBody_IsCrmUnavailable()
    {
        // A sign-in page in front of the fork (Cloudflare Access, for one) answers 200 with a body
        // that is not the fork's reply.
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.OK) { Folder = "Hub" };

        var refused = await Assert.ThrowsAsync<CrmUnavailableException>(
            () => Users(wire).CreateUserAsync("Ann Lee", "ann.lee@spiritfitness.test", Cancel));
        Assert.Equal("CRM did not answer.", refused.Message);
    }

    [Fact]
    public async Task AForkThatNeverAnswers_IsCrmUnavailable()
    {
        var users = new CrmUsers(
            new HttpClient(new HangingHandler()) { Timeout = TimeSpan.FromMilliseconds(50) },
            Options.Create(new TwentyOptions { BaseUrl = "http://twenty.test", HubSecret = "hub-secret" }),
            Options.Create(new HubOptions { CrmUrl = "https://crm.spirit.test" }),
            new TestTimeProvider(DateTimeOffset.UtcNow));

        var refused = await Assert.ThrowsAsync<CrmUnavailableException>(
            () => users.CreateUserAsync("Ann Lee", "ann.lee@spiritfitness.test", Cancel));
        Assert.Equal("CRM did not answer.", refused.Message);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static CrmUsers Users(ReplayingHandler wire) => new(
        new HttpClient(wire),
        Options.Create(new TwentyOptions { BaseUrl = "http://twenty.test", HubSecret = "hub-secret" }),
        Options.Create(new HubOptions { CrmUrl = "https://crm.spirit.test" }),
        new TestTimeProvider(DateTimeOffset.UtcNow));

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
