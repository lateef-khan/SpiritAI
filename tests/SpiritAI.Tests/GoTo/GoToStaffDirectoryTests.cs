using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>
/// The line owners and emails as GoTo listed them on 2026-09-25, cut to two users with test names
/// and emails. Line <c>d3d10a08…</c> (8625) is the one that rang in <c>call_ringing</c>.
/// </summary>
public sealed class GoToStaffDirectoryTests
{
    private const string RingingLine = "d3d10a08-2269-4872-a904-261c68494270";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALineIsMatchedToItsOwnersEmailThroughTheUserKey()
    {
        var wire = new ReplayingHandler(["users", "admin_users"]) { Folder = "GoTo" };
        await using var services = GoToTestServices.Build(wire);
        var directory = services.GetRequiredService<GoToStaffDirectory>();

        Assert.Equal("dana@spiritfitness.com", await directory.FindEmailAsync(RingingLine, Cancel));
        Assert.Equal("sam@spiritfitness.com", await directory.FindEmailAsync("a9770485-8b07-4b04-9086-4238d4484c03", Cancel));
        Assert.Null(await directory.FindEmailAsync("not-a-line", Cancel));

        Assert.Equal(
            [
                "GET https://api.goto.com/users/v1/users?accountKey=1234567890123456789",
                "GET https://api.getgo.com/admin/rest/v1/accounts/1234567890123456789/users?attributes=key,email&offset=0&pageSize=1000",
            ],
            wire.Requests.Select(r => $"{r.Method} {r.Url}"));
        Assert.All(wire.Requests, r => Assert.Equal("Bearer fake-goto-access-token", r.Authorization));
    }
}
