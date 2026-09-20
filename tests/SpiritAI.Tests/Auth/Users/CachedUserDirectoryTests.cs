using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Auth.Users;

using Xunit;

using ZiggyCreatures.Caching.Fusion;

namespace SpiritAI.Tests.Auth.Users;

/// <summary>
/// What the directory's cache remembers, on the engine the host runs: FusionCache behind
/// <see cref="HybridCache"/>.
/// </summary>
public sealed class CachedUserDirectoryTests
{
    private static readonly AuthUser Tiana = new("u-1", "Tiana Bills", "tiana@example.com");

    [Fact]
    public async Task ASecondAskForTheSamePersonReachesNoDirectory()
    {
        var inner = new CountingDirectory(Tiana);
        var directory = Directory(inner);

        await directory.FindByEmailAsync("tiana@example.com", TestContext.Current.CancellationToken);
        var again = await directory.FindByEmailAsync("tiana@example.com", TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.Asked);
        Assert.Equal(Tiana, again);
    }

    [Fact]
    public async Task TwoSpellingsOfOneAddressShareAnEntry()
    {
        var inner = new CountingDirectory(Tiana);
        var directory = Directory(inner);

        await directory.FindByEmailAsync("tiana@example.com", TestContext.Current.CancellationToken);
        var again = await directory.FindByEmailAsync("Tiana@Example.com", TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.Asked);
        Assert.Equal(Tiana, again);
    }

    [Fact]
    public async Task APersonNobodyKnowsIsAskedAboutAgain()
    {
        var inner = new CountingDirectory();
        var directory = Directory(inner);

        Assert.Null(await directory.FindByEmailAsync("nobody@example.com", TestContext.Current.CancellationToken));
        Assert.Null(await directory.FindByEmailAsync("nobody@example.com", TestContext.Current.CancellationToken));

        Assert.Equal(2, inner.Asked);
    }

    private static CachedUserDirectory Directory(IUserDirectory inner)
        => new(
            inner,
            new ServiceCollection()
                .AddFusionCache()
                .AsHybridCache()
                .Services
                .BuildServiceProvider()
                .GetRequiredService<HybridCache>());

    /// <summary>A directory that counts how often it is asked.</summary>
    private sealed class CountingDirectory(params AuthUser[] people) : IUserDirectory
    {
        private readonly FakeUserDirectory _people = new(people);

        public int Asked { get; private set; }

        public ValueTask<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            Asked++;

            return _people.FindByEmailAsync(email, cancellationToken);
        }
    }
}
