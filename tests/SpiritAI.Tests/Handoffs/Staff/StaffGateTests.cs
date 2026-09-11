using System.Security.Claims;

using SpiritAI.Auth.Users;
using SpiritAI.Handoffs;
using SpiritAI.Handoffs.Staff;
using SpiritAI.Tests.Auth.Users;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Staff;

/// <summary>
/// Who is staff — anyone with a Neon sign-in — and what the visitor gets to call them.
/// </summary>
public sealed class StaffGateTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly StaffGate _gate = new(new FakeUserDirectory(
        new AuthUser("user_dana", "Dana Rivera", "dana@example.com"),
        new AuthUser("user_sam", "Sam", "sam@example.com"),
        new AuthUser("user_mj", "  Mary Jo  Smith ", "mj@example.com")));

    [Fact]
    public async Task AnyoneInTheDirectoryIsStaffAndTheVisitorSeesFirstNameAndInitial()
    {
        var member = await _gate.MemberOfAsync(SignedIn("Dana@Example.com"), Cancel);

        Assert.Equal(new HandoffStaffMember("dana@example.com", "Dana R."), member);
    }

    [Fact]
    public async Task AOneWordNameIsShownAsItIs()
    {
        var member = await _gate.MemberOfAsync(SignedIn("sam@example.com"), Cancel);

        Assert.Equal("Sam", member?.Name);
    }

    [Fact]
    public async Task TheInitialComesFromTheLastWord()
    {
        var member = await _gate.MemberOfAsync(SignedIn("mj@example.com"), Cancel);

        Assert.Equal("Mary S.", member?.Name);
    }

    [Fact]
    public async Task SomeoneNotInTheDirectoryIsNobody()
    {
        Assert.Null(await _gate.MemberOfAsync(SignedIn("dealer@example.com"), Cancel));
    }

    [Fact]
    public async Task ACallerWithNoEmailIsNobody()
    {
        var noEmail = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user_x")], "test"));

        Assert.Null(await _gate.MemberOfAsync(noEmail, Cancel));
        Assert.Null(await _gate.MemberOfAsync(null, Cancel));
    }

    private static ClaimsPrincipal SignedIn(string email)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "test"));
}
