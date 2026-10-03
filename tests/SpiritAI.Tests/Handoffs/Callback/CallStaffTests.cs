using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;
using SpiritAI.Handoffs.Callback;
using SpiritAI.Tests.Database;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Callback;

/// <summary>GoTo email → Neon user → Desk link (access spec section 7.4, ruling R19).</summary>
[Collection(PostgresCollection.Name)]
public sealed class CallStaffTests(PostgresFixture fixture) : IAsyncDisposable
{
    private static readonly GoToCallLine DanasLine = new("d3d10a08-2269-4872-a904-261c68494270", "8625", "CONNECTED");

    private readonly ServiceProvider _goto = GoToTestServices.Build(new ReplayingHandler(["users", "admin_users"]) { Folder = "GoTo" });

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheLinesOwner_IsFoundByEmailInAnyCase_ThroughTheirDeskLink()
    {
        await DeskStaffSeed.DanaAsync(fixture);

        var agent = await Staff().FindAgentAsync(DanasLine, Cancel);

        Assert.Equal((3, "Dana Test"), (agent!.Id, agent.Name));
    }

    [Fact]
    public async Task AnUnreadyDeskLink_IsNoAgent()
    {
        await DeskStaffSeed.DanaAsync(fixture, ready: false);

        Assert.Null(await Staff().FindAgentAsync(DanasLine, Cancel));
    }

    [Fact]
    public async Task NoSpiritPerson_IsNoAgent()
    {
        await DeskStaffSeed.NoDanaAsync(fixture);

        Assert.Null(await Staff().FindAgentAsync(DanasLine, Cancel));
    }

    public ValueTask DisposeAsync() => _goto.DisposeAsync();

    private CallStaff Staff() => new(fixture.Open(), _goto.GetRequiredService<GoToStaffDirectory>());
}
