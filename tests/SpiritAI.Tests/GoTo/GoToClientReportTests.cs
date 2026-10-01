using System.Net;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>The call-report calls, sent through the host's own GoTo registration.</summary>
public sealed class GoToClientReportTests
{
    private const string Window =
        "https://api.goto.com/call-events-report/v1/report-summaries?accountKey=1234567890123456789&startTime=2026-09-30T15:00:00.000Z&endTime=2026-09-30T15:05:00.000Z&pageSize=1000";

    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-30T15:00:00Z");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AWindowOfCallsIsListedWithTheNextPageMarker()
    {
        var wire = new ReplayingHandler("report_summaries_page1") { Folder = "GoTo" };

        var page = await Client(wire).ListReportSummariesAsync(Start, Start.AddMinutes(5), pageMarker: null, Cancel);

        Assert.Equal(Window, Assert.Single(wire.Requests).Url);
        Assert.Equal(
            [
                new GoToReportSummary("702e4d82-c7e6-30a0-90f0-ce7eaf8f9b4f", true),
                new GoToReportSummary("c877ee77-0a08-37cf-873a-7ea644e35398", true),
                new GoToReportSummary("371a4e39-5919-3ff1-9ccd-12c647413bd7", true),
            ],
            page.Calls);
        Assert.Equal("page-2", page.NextPageMarker);
    }

    [Fact]
    public async Task TheNextPageIsAskedForByItsMarker()
    {
        var wire = new ReplayingHandler("report_summaries_page2") { Folder = "GoTo" };

        var page = await Client(wire).ListReportSummariesAsync(Start, Start.AddMinutes(5), "page-2", Cancel);

        Assert.Equal(Window + "&pageMarker=page-2", Assert.Single(wire.Requests).Url);
        Assert.Null(page.NextPageMarker);
    }

    [Fact]
    public void AStaffToStaffCallIsNeverWorthAReport()
    {
        // 91f717b0… is outbound with no outside number: a staff member calling a colleague.
        var page = GoToReportSummaryPage.Read(GoToPayloads.Read("report_summaries_page2"));

        Assert.Equal(
            [
                new GoToReportSummary("91f717b0-e95b-3be7-8f10-cd7c00462961", false),
                new GoToReportSummary("2597f380-a5b5-3023-9a3c-10eea86df80f", true),
                new GoToReportSummary("310d5126-4983-3288-b054-a5412f03b37b", true),
            ],
            page.Calls);
    }

    [Fact]
    public async Task OneReportIsReadByItsCallId()
    {
        var wire = new ReplayingHandler("report_transfer") { Folder = "GoTo" };

        var report = await Client(wire).ReadReportAsync("d51fe878-4589-3812-b625-6895cdbd83be", Cancel);

        Assert.Equal("d51fe878-4589-3812-b625-6895cdbd83be", report?.Id);
        Assert.Equal(
            "https://api.goto.com/call-events-report/v1/reports/d51fe878-4589-3812-b625-6895cdbd83be",
            Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task AReportGoToDoesNotHaveYetReadsAsNone()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.NotFound) { Folder = "GoTo" };

        Assert.Null(await Client(wire).ReadReportAsync("not-ended-yet", Cancel));
    }

    [Fact]
    public async Task EachCompanyLineIsMatchedToTheDialPlanItRings()
    {
        var wire = new ReplayingHandler(["voice_extensions", "voice_phone_numbers"]) { Folder = "GoTo" };

        var lines = await Client(wire).ListCompanyLinesAsync(Cancel);

        Assert.Equal("Spirit Start", lines["+18002588511"]);
        Assert.Equal("Sole Start", lines["+18666976531"]);
        Assert.Equal(
            [
                "https://api.goto.com/voice-admin/v1/extensions?accountKey=1234567890123456789&pageSize=100",
                "https://api.goto.com/voice-admin/v1/phone-numbers?accountKey=1234567890123456789&pageSize=100",
            ],
            wire.Requests.Select(r => r.Url));
    }

    [Fact]
    public async Task ANumberThatRingsAPersonOrAQueueHasNoBrand()
    {
        var extensions = JsonNode.Parse(GoToPayloads.Read("voice_extensions").GetRawText())!.AsObject();
        extensions["items"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "11111111-1111-4111-8111-111111111111",
            ["name"] = "Test Person 9999",
            ["type"] = "DIRECT_EXTENSION",
        });

        var numbers = JsonNode.Parse(GoToPayloads.Read("voice_phone_numbers").GetRawText())!.AsObject();
        numbers["items"]!.AsArray().Add(new JsonObject
        {
            ["number"] = "+19995550123",
            ["routeTo"] = new JsonObject { ["id"] = "11111111-1111-4111-8111-111111111111", ["type"] = "EXTENSION" },
        });

        var wire = new AnsweringHandler(extensions.ToJsonString(), numbers.ToJsonString());

        var lines = await Client(wire).ListCompanyLinesAsync(Cancel);

        Assert.DoesNotContain("+19995550123", lines.Keys);
        Assert.Equal("Spirit Start", lines["+18002588511"]);
    }

    private static GoToClient Client(HttpMessageHandler wire)
        => GoToTestServices.Build(wire).GetRequiredService<GoToClient>();
}
