using System.Security.Claims;

using SpiritAI.Auth;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// Which string a call is filed under.
/// </summary>
public sealed class CallerPrincipalTests
{
    [Fact]
    public void IsTheTokenSubject()
    {
        var user = Signed(new Claim(ClaimTypes.NameIdentifier, "user_123"));

        Assert.Equal("user:user_123", CallerPrincipal.KeyOf(user));
    }

    [Fact]
    public void IsNotTheEmail()
    {
        var user = Signed(
            new Claim(ClaimTypes.NameIdentifier, "user_123"),
            new Claim(ClaimTypes.Email, "someone@example.com"));

        Assert.DoesNotContain("example.com", CallerPrincipal.KeyOf(user), StringComparison.Ordinal);
    }

    [Fact]
    public void IsAbsentWhenTheTokenNamesNoSubject()
    {
        var user = Signed(new Claim(ClaimTypes.Email, "someone@example.com"));

        Assert.Null(CallerPrincipal.KeyOf(user));
    }

    [Fact]
    public void IsAbsentForAnAnonymousCaller()
    {
        Assert.Null(CallerPrincipal.KeyOf(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private static ClaimsPrincipal Signed(params Claim[] claims)
        => new(new ClaimsIdentity(claims, NeonAuthenticationDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role));
}
