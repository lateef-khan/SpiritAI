using SpiritAI.Auth.Users;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Auth.Users;

/// <summary>
/// The directory over <c>neon_auth."user"</c>, against real PostgreSQL.
/// </summary>
public sealed class NeonUserDirectoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindsAPersonByEmailWhateverTheCase()
    {
        await using var database = fixture.Open();
        await NeonAuthUserTable.EnsureAsync(database);
        var email = $"dana-{Guid.NewGuid():N}@example.com";
        var id = await NeonAuthUserTable.InsertAsync(database, "Dana Rivera", email);

        try
        {
            var directory = new NeonUserDirectory(database);

            var user = await directory.FindByEmailAsync(email.ToUpperInvariant(), Cancel);

            Assert.Equal(new AuthUser(id.ToString(), "Dana Rivera", email), user);
        }
        finally
        {
            await NeonAuthUserTable.DeleteAsync(database, id);
        }
    }

    [Fact]
    public async Task ABannedPersonIsNotFound()
    {
        await using var database = fixture.Open();
        await NeonAuthUserTable.EnsureAsync(database);
        var email = $"banned-{Guid.NewGuid():N}@example.com";
        var id = await NeonAuthUserTable.InsertAsync(database, "Nobody Now", email, banned: true);

        try
        {
            var directory = new NeonUserDirectory(database);

            Assert.Null(await directory.FindByEmailAsync(email, Cancel));
        }
        finally
        {
            await NeonAuthUserTable.DeleteAsync(database, id);
        }
    }

    [Fact]
    public async Task AnUnknownAddressIsNobody()
    {
        await using var database = fixture.Open();
        await NeonAuthUserTable.EnsureAsync(database);
        var directory = new NeonUserDirectory(database);

        Assert.Null(await directory.FindByEmailAsync($"nobody-{Guid.NewGuid():N}@example.com", Cancel));
    }
}
