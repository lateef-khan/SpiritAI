using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Database;
using SpiritAI.Neon;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Neon;

/// <summary>The two Neon Auth calls, against the answers Neon gave on 2026-10-03 (ruling R13).</summary>
[Collection(PostgresCollection.Name)]
public sealed class NeonUsersTests(PostgresFixture fixture)
{
    private const string Users = "https://console.neon.tech/api/v2/projects/proj-1/branches/br-1/auth/users";

    private static readonly Guid Created = new("99aed9ca-a881-4f10-9a52-6bc31196131d");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_PostsTheEmailAndName_WithTheKey_AndAnswersTheId()
    {
        var neon = new ReplayingHandler([("neon_user_created", HttpStatusCode.Created)]) { Folder = "Neon" };

        var id = await Users_(neon).CreateAsync("ann@spiritfitness.test", "Ann Lee", Cancel);

        Assert.Equal(Created, id);
        var request = Assert.Single(neon.Requests);
        Assert.Equal(("POST", Users, "Bearer key-1"), (request.Method, request.Url, request.Authorization));
        Assert.Equal("""{"email":"ann@spiritfitness.test","name":"Ann Lee"}""", request.Body);
    }

    [Fact]
    public async Task Create_ForAnEmailNeonHas_IsEmailTaken()
    {
        var neon = new ReplayingHandler([("neon_user_exists", HttpStatusCode.BadRequest)]) { Folder = "Neon" };

        await Assert.ThrowsAsync<NeonEmailTakenException>(() => Users_(neon).CreateAsync("ann@spiritfitness.test", "Ann", Cancel));
    }

    [Fact]
    public async Task Create_WhenNeonFails_IsUnavailable()
    {
        var neon = new ReplayingHandler([(null, HttpStatusCode.InternalServerError)]) { Folder = "Neon" };

        var thrown = await Assert.ThrowsAsync<NeonUnavailableException>(() => Users_(neon).CreateAsync("ann@spiritfitness.test", "Ann", Cancel));
        Assert.Equal("Neon did not answer.", thrown.Message);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.NoContent)]
    [InlineData("neon_user_not_found", HttpStatusCode.NotFound)]
    public async Task Delete_IsDone_WhenNeonDeletesOrNeverHadThem(string? payload, HttpStatusCode status)
    {
        var neon = new ReplayingHandler([(payload, status)]) { Folder = "Neon" };

        await Users_(neon).DeleteAsync(Created, Cancel);

        var request = Assert.Single(neon.Requests);
        Assert.Equal(("DELETE", $"{Users}/{Created}"), (request.Method, request.Url));
    }

    [Fact]
    public async Task WithNoKey_NothingIsCalled_AndTheWordsSaySo()
    {
        var neon = new ReplayingHandler("neon_user_created") { Folder = "Neon" };

        var thrown = await Assert.ThrowsAsync<NeonUnavailableException>(
            () => Users_(neon, new NeonOptions { ProjectId = "proj-1", BranchId = "br-1" }).CreateAsync("a@b.test", "A", Cancel));

        Assert.Equal("Neon is not set up yet.", thrown.Message);
        Assert.Empty(neon.Requests);
    }

    [Fact]
    public async Task CopyToLocal_WritesThenRemovesTheLocalRow()
    {
        await using (var db = fixture.Open())
        {
            await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {Created}""", Cancel);
        }

        var neon = new ReplayingHandler([("neon_user_created", HttpStatusCode.Created), (null, HttpStatusCode.NoContent)]) { Folder = "Neon" };
        await using var local = fixture.Open();
        var users = Users_(neon, copyToLocal: true, db: local);
        var email = $"{Guid.NewGuid():N}@spiritfitness.test";

        await users.CreateAsync(email, "Copied", Cancel);
        Assert.Equal(email, await EmailOfAsync(Created));

        await users.DeleteAsync(Created, Cancel);
        Assert.Null(await EmailOfAsync(Created));
    }

    private async Task<string?> EmailOfAsync(Guid id)
    {
        await using var db = fixture.Open();
        return await db.Database.SqlQuery<string>($"""SELECT email AS "Value" FROM neon_auth."user" WHERE id = {id}""").SingleOrDefaultAsync(Cancel);
    }

    private static NeonOptions Configured() => new() { ApiKey = "key-1", ProjectId = "proj-1", BranchId = "br-1" };

    private static NeonUsers Users_(ReplayingHandler neon, NeonOptions? options = null, bool copyToLocal = false, SpiritDbContext? db = null)
    {
        var settings = options ?? Configured();
        settings.CopyToLocal = copyToLocal;
        return new NeonUsers(new HttpClient(neon), Options.Create(settings), db!);
    }
}
