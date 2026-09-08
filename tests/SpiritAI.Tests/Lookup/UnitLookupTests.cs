using System.Text.Json;

using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// The shaping between DAB's rowsets and the unit panel.
/// </summary>
/// <remarks>
/// Every payload here is the shape production answered with, trimmed. A stored procedure wraps its
/// rows in <c>value.value</c> and <c>read_records</c> wraps them in <c>result.value</c>; getting
/// that wrong reads as a section that is simply empty, which is the failure these tests exist to
/// make loud.
/// </remarks>
public sealed class UnitLookupTests
{
    private const string Serial = "5808881004036047";

    /// <summary>One service call, closed, with an order on it.</summary>
    private const string HistoryJson = """
    {
      "entity": "GetServiceHistoryBySn",
      "status": "success",
      "value": { "value": [
        {
          "SerialNo": "5808881004036047", "ModelNo": "580888", "ModelName": "SOLE WF80 2010",
          "Sole": true, "FG": "TREADMILL", "ModelVersion": 2, "MfgDate": "04/2010",
          "PurchasedDate": "2010-10-23T00:00:00", "SetupDate": "2010-09-01T12:25:00",
          "ServiceId": 796955, "CallDate": "2025-01-15T15:18:00",
          "ServiceDate": "2025-01-15T16:21:00", "ServiceRep": "tiana.bills",
          "Description": "Missing Parts", "Solution": null,
          "ServiceStatus": "CLOSED", "OrderId": 1, "CaseStatus": "CLOSED"
        },
        {
          "SerialNo": "5808881004036047", "ModelNo": "580888", "ModelName": "SOLE WF80 2010",
          "Sole": true, "FG": "TREADMILL", "ModelVersion": 2,
          "ServiceId": 800001, "CallDate": "2026-02-01T09:00:00",
          "ServiceRep": "sam", "Description": "Belt slips",
          "ServiceStatus": "IN PROGRESS", "OrderId": 2, "CaseStatus": "IN PROGRESS"
        }
      ] }
    }
    """;

    private const string PartsJson = """
    {
      "entity": "GetPartsBySn",
      "status": "success",
      "value": { "value": [
        {
          "SerialNo": "5808881004036047", "ModelNo": "580888", "ModelVersion": 2,
          "ModelName": "SOLE WF80 2010", "DyacoNo": "", "SpNo": "J99A0002",
          "Description": "HARDWARE KIT", "Qty": 1
        }
      ] }
    }
    """;

    private const string WarrantyJson = """
    {
      "entity": "ModelWarranty",
      "result": { "value": [
        { "ModelNo": "580888", "Version": 1, "LaborPeriod": 365, "Part2Period": 1000, "Motor": 36500 },
        { "ModelNo": "580888", "Version": 2, "LaborPeriod": 730, "Part2Period": 1825, "Motor": 36500 }
      ] }
    }
    """;

    private const string ErrorJson = """
    {
      "toolName": "get_service_history_by_sn",
      "status": "error",
      "error": { "type": "ExecutionError", "message": "While processing your request the database ran into an error." }
    }
    """;

    private const string OrderJson = """
    {
      "entity": "GetWorkOrders",
      "status": "success",
      "value": { "value": [
        {
          "ServiceId": 796955, "OrderId": 1, "OrderNumber": "796955-1",
          "OrderDate": "2025-01-15T16:21:18.107", "AppointDate": null,
          "Shippeddate": "2025-01-16T00:00:00", "ClosedDate": null,
          "OrderType": "Warranty", "ISPName": null, "ISPStatus": "No", "Tech": "tiana.bills",
          "DealerNo": null, "Trackno": "284431256500", "Notes": "Kurtis M.",
          "PartNo": "K140002-Z3", "PartDesc": "ROLLER, REAR", "ItemShipped": 1, "IsReturned": true
        },
        {
          "ServiceId": 796955, "OrderId": 1, "OrderNumber": "796955-1",
          "OrderDate": "2025-01-15T16:21:18.107", "OrderType": "Warranty",
          "PartNo": null, "PartDesc": null, "ItemShipped": null, "IsReturned": false
        }
      ] }
    }
    """;

    [Theory]
    [InlineData("5808881004036047", true)]
    [InlineData("580888100403604", false)]
    [InlineData("58088810040360477", false)]
    [InlineData("58088810040360a7", false)]
    [InlineData("", false)]
    public void ASerialIsSixteenDigits(string text, bool expected)
        => Assert.Equal(expected, UnitLookup.IsSerial(text));

    [Theory]
    [InlineData("845435-1", true)]
    [InlineData("845435", false)]
    [InlineData("845435-", false)]
    [InlineData("-1", false)]
    [InlineData("845435-1-2", false)]
    public void AnOrderNumberIsDigitsADashAndDigits(string text, bool expected)
        => Assert.Equal(expected, UnitLookup.IsOrderNumber(text));

    [Fact]
    public async Task AFullResultFillsEverySection()
    {
        var unit = await Lookup().ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.NotNull(unit);
        Assert.Empty(unit.Unavailable);
        Assert.Equal("580888", unit.Header!.ModelNo);
        Assert.Equal("SOLE WF80 2010", unit.Header.ModelName);
        Assert.Equal("TREADMILL", unit.Header.Category);
        Assert.Equal("04/2010", unit.Header.ManufacturedOn);
        Assert.True(unit.Header.IsSole);
        Assert.Equal(2, unit.History!.Count);
        Assert.Equal("J99A0002", Assert.Single(unit.Parts!).SpNo);
    }

    [Fact]
    public async Task JobsAreTheCallsThatAreNotClosed()
    {
        var unit = await Lookup().ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        var job = Assert.Single(unit!.Jobs!);

        Assert.Equal("800001-2", job.OrderNumber);
        Assert.Equal(JobStatus.Open, job.Status);

        // The word is kept beside the enum, because the enum is derived and this column is not
        // spelled one way in this database.
        Assert.Equal("IN PROGRESS", job.StatusText);
    }

    [Fact]
    public async Task HistoryIsNewestFirst()
    {
        var unit = await Lookup().ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.Equal("800001-2", unit!.History![0].OrderNumber);
        Assert.Equal("796955-1", unit.History[1].OrderNumber);
    }

    [Fact]
    public async Task WarrantyCountsForwardFromThePurchaseDateOfTheRightVersion()
    {
        var unit = await Lookup().ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        var labor = Assert.Single(unit!.Warranty!, term => term.Category == "Labor");

        // Version 2 of this model carries 730 days, not version 1's 365.
        Assert.Equal(730, labor.Days);
        Assert.Equal(new DateTimeOffset(2012, 10, 22, 0, 0, 0, TimeSpan.Zero), labor.ExpiresOn);
        Assert.False(labor.IsCovered);
    }

    [Fact]
    public async Task OneToolFailingNamesItsSectionAndLeavesTheRest()
    {
        var unit = await Lookup(parts: ErrorJson).ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.NotNull(unit);
        Assert.Equal([UnitSection.Parts], unit.Unavailable);
        Assert.Null(unit.Parts);
        Assert.NotNull(unit.History);
        Assert.NotNull(unit.Header);
    }

    [Fact]
    public async Task OneToolThrowingIsTreatedTheSameWay()
    {
        var lookup = new UnitLookup((toolId, _, _) => toolId == "get_parts_by_sn"
            ? throw new HttpRequestException("DAB is not answering.")
            : ValueTask.FromResult(Json(toolId switch
            {
                "get_service_history_by_sn" => HistoryJson,
                _ => WarrantyJson,
            })));

        var unit = await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.Equal([UnitSection.Parts], unit!.Unavailable);
        Assert.NotNull(unit.History);
    }

    [Fact]
    public async Task ASerialNobodySoldIsNotFound()
    {
        var unit = await Lookup(history: ErrorJson, parts: ErrorJson)
            .ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        // Both of the two tools that know a serial refused it. One refusing alone is a section that
        // could not be read; both refusing is a machine that does not exist.
        Assert.Null(unit);
    }

    [Fact]
    public async Task AToolWrappingItsRowsInContentIsReadTheSameWay()
    {
        var wrapped = JsonSerializer.SerializeToElement(
            new { content = new[] { new { type = "text", text = HistoryJson } } });

        var lookup = new UnitLookup((toolId, _, _) => ValueTask.FromResult(toolId switch
        {
            "get_service_history_by_sn" => wrapped,
            "get_parts_by_sn" => Json(PartsJson),
            _ => Json(WarrantyJson),
        }));

        var unit = await lookup.ReadUnitAsync(Serial, TestContext.Current.CancellationToken);

        Assert.Equal(2, unit!.History!.Count);
    }

    [Fact]
    public async Task AnOrderKeepsOnlyItsRealPartLines()
    {
        var lookup = new UnitLookup((_, _, _) => ValueTask.FromResult(Json(OrderJson)));

        var order = await lookup.ReadOrderAsync("796955-1", TestContext.Current.CancellationToken);

        Assert.NotNull(order);
        Assert.Equal("796955-1", order.OrderNumber);
        Assert.Equal("Warranty", order.OrderType);
        Assert.Equal("tiana.bills", order.Technician);
        Assert.Null(order.ClosedOn);

        // The rowset is flat and repeats the order on every line, so the second row -- which carries
        // no part -- is an order with nothing on it rather than a line of its own.
        var line = Assert.Single(order.Lines);
        Assert.Equal("K140002-Z3", line.PartNo);
        Assert.True(line.IsReturned);
    }

    [Fact]
    public async Task AnOrderNumberNothingCarriesIsNotFound()
    {
        var lookup = new UnitLookup((_, _, _) => ValueTask.FromResult(Json(ErrorJson)));

        Assert.Null(await lookup.ReadOrderAsync("1-1", TestContext.Current.CancellationToken));
    }

    /// <summary>A lookup whose tools answer canned payloads.</summary>
    /// <param name="history">What <c>get_service_history_by_sn</c> answers.</param>
    /// <param name="parts">What <c>get_parts_by_sn</c> answers.</param>
    /// <param name="warranty">What <c>read_records</c> answers.</param>
    /// <returns>The lookup under test.</returns>
    private static UnitLookup Lookup(string? history = null, string? parts = null, string? warranty = null)
        => new((toolId, _, _) => ValueTask.FromResult(Json(toolId switch
        {
            "get_service_history_by_sn" => history ?? HistoryJson,
            "get_parts_by_sn" => parts ?? PartsJson,
            _ => warranty ?? WarrantyJson,
        })));

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
}
