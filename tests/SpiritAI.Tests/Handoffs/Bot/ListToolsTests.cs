using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Tests.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>
/// The teams and contact fields a handoff picks from, as the service user reads them from a live
/// Chatwoot, kept so a second handoff does not read them again.
/// </summary>
public sealed class ListToolsTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TeamsAreReadOnceAndKept()
    {
        var wire = new ReplayingHandler("teams");
        var tool = new ListTeamsTool(Client(wire), TestHybridCache.Create());

        await tool.ListAsync(Cancel);
        var answer = await tool.ListAsync(Cancel);

        Assert.Equal([new ChatwootTeam(2, "probe repairs", "Broken machines and parts")], answer.Teams);
        Assert.Single(wire.Requests);
    }

    [Fact]
    public async Task ContactFieldsAreReadOnceAndKept()
    {
        var wire = new ReplayingHandler("contact_attribute_definitions");
        var tool = new ListContactFieldsTool(Client(wire), TestHybridCache.Create());

        await tool.ListAsync(Cancel);
        var answer = await tool.ListAsync(Cancel);

        Assert.Equal([new ChatwootContactField("probe_serial", "Probe Serial", "text", "The serial number on the frame")], answer.Fields);
        Assert.Single(wire.Requests);
    }

    private static ChatwootClient Client(ReplayingHandler wire)
        => new(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            ServiceToken = "service-token",
        }));
}
