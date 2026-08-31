using Xunit;

namespace SpiritAI.Tests.Auth;

/// <summary>The key set, and the two things about it that are not obvious: it caches, and it
/// refuses to be turned into a way of hammering Neon.</summary>
public sealed class NeonSigningKeysTests
{
    [Fact]
    public async Task FetchesOnce_ThenServesFromMemory()
    {
        var kit = new NeonAuthTestKit();
        var keys = kit.SigningKeys();

        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(await keys.FindAsync(NeonAuthTestKit.KeyId, TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, kit.JwksFetches);
    }

    [Fact]
    public async Task DoesNotRefetch_ForEveryUnknownKey()
    {
        var kit = new NeonAuthTestKit();
        var keys = kit.SigningKeys();

        // A run of tokens naming keys that do not exist is what an attacker sends. One fetch is
        // the rotation check; the rest must cost Neon nothing.
        for (var i = 0; i < 20; i++)
        {
            Assert.Null(await keys.FindAsync($"made-up-{i}", TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, kit.JwksFetches);
    }

    [Fact]
    public async Task Refetches_OnceTheCooldownHasPassed()
    {
        var kit = new NeonAuthTestKit();
        var keys = kit.SigningKeys();

        Assert.Null(await keys.FindAsync("made-up", TestContext.Current.CancellationToken));
        kit.Clock.Now = kit.Clock.Now.Add(kit.Options.KeyRefreshCooldown).AddSeconds(1);
        Assert.Null(await keys.FindAsync("made-up", TestContext.Current.CancellationToken));

        // Two, because a key really can be rotated in and this is how the host notices.
        Assert.Equal(2, kit.JwksFetches);
    }
}
