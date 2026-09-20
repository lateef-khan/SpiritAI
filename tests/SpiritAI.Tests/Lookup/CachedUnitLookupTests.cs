using System.Text.Json;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.Lookup;

using Xunit;

using ZiggyCreatures.Caching.Fusion;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// What the panel's cache remembers, on the engine the host runs: FusionCache behind
/// <see cref="HybridCache"/>.
/// </summary>
public sealed class CachedUnitLookupTests
{
    private const string Serial = "5808881004036047";

    /// <summary>The least DAB answers with that still makes a whole panel: one row per tool.</summary>
    private const string HistoryJson = """
    { "status": "success", "value": { "value": [
      { "SerialNo": "5808881004036047", "ModelNo": "580888", "ModelVersion": 2, "ModelName": "SOLE WF80 2010",
        "PurchasedDate": "2010-10-23T00:00:00", "ServiceId": 796955, "OrderId": 1, "CaseStatus": "CLOSED" }
    ] } }
    """;

    private const string PartsJson = """
    { "status": "success", "value": { "value": [
      { "SerialNo": "5808881004036047", "ModelNo": "580888", "ModelVersion": 2, "SpNo": "J99A0002", "Qty": 1 }
    ] } }
    """;

    private const string WarrantyJson = """
    { "entity": "ModelWarranty", "result": { "value": [
      { "ModelNo": "580888", "Version": 2, "LaborPeriod": 730 }
    ] } }
    """;

    private const string OrderJson = """
    { "status": "success", "value": { "value": [
      { "ServiceId": 796955, "OrderId": 1, "OrderNumber": "796955-1", "OrderType": "Warranty", "PartNo": "K140002-Z3" }
    ] } }
    """;

    private const string ErrorJson = """
    { "status": "error", "error": { "type": "ExecutionError", "message": "the database ran into an error." } }
    """;

    [Fact]
    public async Task ASecondReadOfTheSameMachineReachesNoTool()
    {
        var calls = 0;
        var lookup = Lookup(_ => { calls++; return null; });

        await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken);
        var callsAfterFirst = calls;
        var second = await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.Equal(callsAfterFirst, calls);
        Assert.Equal("SOLE WF80 2010", second!.Header!.ModelName);
        Assert.Equal(new DateTimeOffset(2010, 10, 23, 0, 0, 0, TimeSpan.Zero), second.Header.PurchasedOn);
        Assert.Equal("J99A0002", Assert.Single(second.Parts!).SpNo);
        Assert.Equal("Labor", Assert.Single(second.Warranty!).Category);
    }

    [Fact]
    public async Task APanelWithASectionMissingIsReadAgain()
    {
        var historyCalls = 0;
        var lookup = Lookup(toolId => toolId == "get_service_history_by_sn" && ++historyCalls == 1 ? ErrorJson : null);

        var broken = await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken);
        var whole = await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.Contains(UnitSection.History, broken!.Unavailable);
        Assert.Empty(whole!.Unavailable);
    }

    [Fact]
    public async Task AMachineNobodySoldIsReadAgain()
    {
        var calls = 0;
        var lookup = Lookup(_ => { calls++; return ErrorJson; });

        Assert.Null(await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken));
        var callsAfterFirst = calls;
        Assert.Null(await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken));

        Assert.True(calls > callsAfterFirst);
    }

    [Fact]
    public async Task ASecondReadOfTheSameOrderReachesNoTool()
    {
        var calls = 0;
        var lookup = Lookup(_ => { calls++; return OrderJson; });

        await lookup.ReadOrderAsync("796955-1", TestContext.Current.CancellationToken);
        var second = await lookup.ReadOrderAsync("796955-1", TestContext.Current.CancellationToken);

        Assert.Equal(1, calls);
        Assert.Equal("K140002-Z3", Assert.Single(second!.Lines).PartNo);
    }

    /// <summary>A cached lookup over canned tools, on a fresh FusionCache.</summary>
    /// <param name="answer">
    /// What a tool answers, by id, or <see langword="null"/> for the whole-panel default.
    /// </param>
    private static CachedUnitLookup Lookup(Func<string, string?> answer)
    {
        var cache = new ServiceCollection()
            .AddFusionCache()
            .AsHybridCache()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();

        UnitLookup inner = new((toolId, _, _) => ValueTask.FromResult(Json(answer(toolId) ?? toolId switch
        {
            "get_service_history_by_sn" => HistoryJson,
            "search_parts" => PartsJson,
            "get_work_orders" => OrderJson,
            _ => WarrantyJson,
        })));

        return new CachedUnitLookup(inner, cache);
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
}
