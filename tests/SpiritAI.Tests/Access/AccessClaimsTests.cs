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
    public async Task TheUsersGroups_BecomeTheCallersGroups()
    {
        var access = new FakeUserAccess([AccessGroup.TechService]);
        using var services = Build(access);
        var userId = Guid.NewGuid();

        var user = await Transform(services, AccessTestUsers.SignedIn(userId));

        Assert.Equal([AccessGroup.TechService], AccessGroups.Of(user));
    }

    [Fact]
    public async Task TransformingTwice_AddsTheGroupsOnce()
    {
        using var services = Build(new FakeUserAccess([AccessGroup.Admin]));
        var user = AccessTestUsers.SignedIn(Guid.NewGuid());

        await Transform(services, user);
        await Transform(services, user);

        Assert.Single(user.Identities, identity => identity.AuthenticationType == AccessGroups.IdentityType);
    }

    [Fact]
    public async Task ARoleChange_ShowsOnceTheCacheEntryIsRemoved()
    {
        var access = new FakeUserAccess([AccessGroup.TechService]);
        using var services = Build(access);
        var userId = Guid.NewGuid();
        await Transform(services, AccessTestUsers.SignedIn(userId));

        access.Groups = [AccessGroup.TechServiceManager];
        var cached = await Transform(services, AccessTestUsers.SignedIn(userId));

        await services.GetRequiredService<HybridCache>().RemoveAsync($"spirit:access:{userId}", TestContext.Current.CancellationToken);
        var fresh = await Transform(services, AccessTestUsers.SignedIn(userId));

        Assert.Equal([AccessGroup.TechService], AccessGroups.Of(cached));
        Assert.Equal([AccessGroup.TechServiceManager], AccessGroups.Of(fresh));
    }

    [Fact]
    public async Task ASubjectThatIsNotANeonUserId_GetsNoGroups()
    {
        var access = new FakeUserAccess([AccessGroup.Admin]);
        using var services = Build(access);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user_123")], "neon"));

        user = await Transform(services, user);

        Assert.Empty(AccessGroups.Of(user));
        Assert.Equal(0, access.Reads);
    }

    private static ServiceProvider Build(FakeUserAccess access)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TestHybridCache.Create());
        services.AddAccess();
        services.AddScoped<IUserAccess>(_ => access);
        return services.BuildServiceProvider();
    }

    private static Task<ClaimsPrincipal> Transform(IServiceProvider services, ClaimsPrincipal user)
        => services.GetRequiredService<IClaimsTransformation>().TransformAsync(user);

    private sealed class FakeUserAccess(AccessGroup[] groups) : IUserAccess
    {
        public AccessGroup[] Groups { get; set; } = groups;

        public int Reads { get; private set; }

        public ValueTask<IReadOnlyList<AccessGroup>> GroupsOfAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult<IReadOnlyList<AccessGroup>>(Groups);
        }
    }
}
