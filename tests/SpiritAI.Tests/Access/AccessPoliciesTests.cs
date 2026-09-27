using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>Each policy lets in its own rank and every rank above it.</summary>
public sealed class AccessPoliciesTests
{
    [Theory]
    [InlineData(AccessPolicies.Staff, AccessGroup.Guest, false)]
    [InlineData(AccessPolicies.Staff, AccessGroup.Dealer, false)]
    [InlineData(AccessPolicies.Staff, AccessGroup.InsideSales, true)]
    [InlineData(AccessPolicies.Staff, AccessGroup.Admin, true)]
    [InlineData(AccessPolicies.Manager, AccessGroup.InsideSalesSupervisor, false)]
    [InlineData(AccessPolicies.Manager, AccessGroup.TechServiceManager, true)]
    [InlineData(AccessPolicies.Admin, AccessGroup.InsideSalesManager, false)]
    [InlineData(AccessPolicies.Admin, AccessGroup.Admin, true)]
    public async Task APolicy_LetsInItsRankAndAbove(string policy, AccessGroup group, bool allowed)
    {
        var result = await Authorize(AccessTestUsers.Holding(group), policy);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task ACallerWithNoGroup_IsLetIntoNothing()
    {
        var result = await Authorize(AccessTestUsers.Holding(), AccessPolicies.Staff);

        Assert.False(result.Succeeded);
    }

    private static async Task<AuthorizationResult> Authorize(ClaimsPrincipal user, string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAccess();
        await using var provider = services.BuildServiceProvider();

        return await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);
    }
}
