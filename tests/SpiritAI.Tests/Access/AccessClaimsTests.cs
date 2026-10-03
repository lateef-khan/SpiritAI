using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Access;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>What <c>AddAccess</c> puts on a signed-in caller, and how long it trusts it.</summary>
public sealed class AccessClaimsTests
{
    [Fact]
    public async Task ThePersonsPermissions_BecomeTheCallersPermissions()
    {
        using var services = Build(new FixedAccess(Permission.ChatAgentStaff, Permission.LookupUnits));

        var user = await Transform(services, AccessTestUsers.SignedIn(Guid.NewGuid()));

        Assert.Equal([Permission.ChatAgentStaff, Permission.LookupUnits], Permissions.Of(user).Order());
    }

    [Fact]
    public async Task ABannedPerson_GetsTheBannedClaim_AndNoPermission()
    {
        using var services = Build(new FixedAccess(Permission.SettingsPeople) { Banned = true });

        var user = await Transform(services, AccessTestUsers.SignedIn(Guid.NewGuid()));

        Assert.True(user.HasClaim(Permissions.BannedClaimType, "true"));
        Assert.Empty(Permissions.Of(user));
    }

    [Fact]
    public async Task TransformingTwice_AddsTheIdentityOnce()
    {
        using var services = Build(new FixedAccess(Permission.LookupOrders));
        var user = AccessTestUsers.SignedIn(Guid.NewGuid());

        await Transform(services, user);
        await Transform(services, user);

        Assert.Single(user.Identities, identity => identity.AuthenticationType == Permissions.IdentityType);
    }

    [Fact]
    public async Task AChange_ShowsOnceTheV3KeyIsRemoved()
    {
        var access = new FixedAccess(Permission.ChatAgentStaff);
        using var services = Build(access);
        var userId = Guid.NewGuid();
        await Transform(services, AccessTestUsers.SignedIn(userId));

        access.Held = [Permission.ChatAgentManager];
        var cached = await Transform(services, AccessTestUsers.SignedIn(userId));

        await services.GetRequiredService<HybridCache>().RemoveAsync($"spirit:access:v3:{userId}", TestContext.Current.CancellationToken);
        var fresh = await Transform(services, AccessTestUsers.SignedIn(userId));

        Assert.Equal([Permission.ChatAgentStaff], Permissions.Of(cached));
        Assert.Equal([Permission.ChatAgentManager], Permissions.Of(fresh));
    }

    [Fact]
    public async Task ASubjectThatIsNotANeonUserId_GetsNothing_AndReadsNothing()
    {
        var access = new FixedAccess(Permission.SettingsPeople);
        using var services = Build(access);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user_123")], "neon"));

        user = await Transform(services, user);

        Assert.Empty(Permissions.Of(user));
        Assert.Equal(0, access.Reads);
    }

    private static ServiceProvider Build(FixedAccess access)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestHybridCache.Create());
        services.AddAccess();
        services.AddScoped<IAccessResolver>(_ => access);
        return services.BuildServiceProvider();
    }

    private static Task<ClaimsPrincipal> Transform(IServiceProvider services, ClaimsPrincipal user)
        => services.GetRequiredService<IClaimsTransformation>().TransformAsync(user);
}
