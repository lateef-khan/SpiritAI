using System.Globalization;
using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.CallLog;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.CallLog;

/// <summary>The start-up catch-up over GoTo's recorded call list for 2026-09-30 15:00–15:05 UTC.</summary>
public sealed class CallLogCatchUpTests
{
    private const string Summaries = "https://api.goto.com/call-events-report/v1/report-summaries?accountKey=1234567890123456789";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T15:05:00Z");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EveryCallWorthAReportOnEveryPageIsQueuedInOrder()
    {
        var wire = new ReplayingHandler(["report_summaries_page1", "report_summaries_page2"]) { Folder = "GoTo" };
        var queue = new CallLogQueue();

        var queued = await CatchUp(wire, queue, new CallLogOptions { CatchUpFrom = Now.AddMinutes(-5) }).RunAsync(Cancel);

        Assert.Equal(5, queued);
        Assert.Equal(
            [
                "702e4d82-c7e6-30a0-90f0-ce7eaf8f9b4f",
                "c877ee77-0a08-37cf-873a-7ea644e35398",
                "371a4e39-5919-3ff1-9ccd-12c647413bd7",
                "2597f380-a5b5-3023-9a3c-10eea86df80f",
                "310d5126-4983-3288-b054-a5412f03b37b",
            ],
            await queue.ReadAllAsync(Cancel).Take(5).ToListAsync(Cancel));
        Assert.Equal(
            [
                $"{Summaries}&startTime=2026-09-30T15:00:00.000Z&endTime=2026-09-30T15:05:00.000Z&pageSize=1000",
                $"{Summaries}&startTime=2026-09-30T15:00:00.000Z&endTime=2026-09-30T15:05:00.000Z&pageSize=1000&pageMarker=page-2",
            ],
            wire.Requests.Select(r => r.Url));
    }

    [Fact]
    public async Task WithNoStartSetItWalksTheWindowOneDayAtATimeOldestFirst()
    {
        var wire = new ReplayingHandler("report_summaries_page2") { Folder = "GoTo" };

        await CatchUp(wire, new CallLogQueue(), new CallLogOptions { CatchUpWindow = TimeSpan.FromHours(30) }).RunAsync(Cancel);

        Assert.Equal(
            [
                $"{Summaries}&startTime=2026-09-29T09:05:00.000Z&endTime=2026-09-30T09:05:00.000Z&pageSize=1000",
                $"{Summaries}&startTime=2026-09-30T09:05:00.000Z&endTime=2026-09-30T15:05:00.000Z&pageSize=1000",
            ],
            wire.Requests.Select(r => r.Url));
    }

    [Fact]
    public async Task APageMarkerThatNeverRunsOutStopsAtTheCapWithAWarningAndTheIdsSoFarQueued()
    {
        var wire = new ReplayingHandler("report_summaries_page1") { Folder = "GoTo" };
        var queue = new CallLogQueue();
        var logger = new CapturingLogger<CallLogCatchUp>();
        var from = Now.AddMinutes(-5);

        var queued = await CatchUp(wire, queue, new CallLogOptions { CatchUpFrom = from }, logger).RunAsync(Cancel);

        // GoToApi.MaxPages
        Assert.Equal(50, wire.Requests.Count);
        Assert.Equal(queued, queue.Waiting);
        Assert.Equal(
            [
                "702e4d82-c7e6-30a0-90f0-ce7eaf8f9b4f",
                "c877ee77-0a08-37cf-873a-7ea644e35398",
                "371a4e39-5919-3ff1-9ccd-12c647413bd7",
            ],
            (await queue.ReadAllAsync(Cancel).Take(queued).ToListAsync(Cancel)).Distinct());

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains($"{ShownUtc(from)} to {ShownUtc(Now)}", warning.Message, StringComparison.Ordinal);
        Assert.Contains("2026-09-30T15:00:00Z to 2026-09-30T15:05:00Z", warning.Exception!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWindowThatFailsIsSkippedWithAWarningAndTheNextOneIsStillQueued()
    {
        var firstDay = Now.AddHours(-30);
        var wire = new RoutingHandler(request => request.RequestUri!.Query.Contains("startTime=2026-09-29T09:05:00.000Z", StringComparison.Ordinal)
            ? (null, HttpStatusCode.InternalServerError)
            : ("report_summaries_page2", HttpStatusCode.OK));
        var queue = new CallLogQueue();
        var logger = new CapturingLogger<CallLogCatchUp>();

        var queued = await CatchUp(wire, queue, new CallLogOptions { CatchUpFrom = firstDay }, logger).RunAsync(Cancel);

        Assert.Equal(2, queued);
        Assert.Contains(wire.Requests, r => r.Url.Contains("startTime=2026-09-30T09:05:00.000Z&endTime=2026-09-30T15:05:00.000Z", StringComparison.Ordinal));
        Assert.Equal(
            ["2597f380-a5b5-3023-9a3c-10eea86df80f", "310d5126-4983-3288-b054-a5412f03b37b"],
            await queue.ReadAllAsync(Cancel).Take(2).ToListAsync(Cancel));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains($"{ShownUtc(firstDay)} to {ShownUtc(firstDay.AddDays(1))}", warning.Message, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(warning.Exception);
    }

    [Fact]
    public async Task AStartMoreThanAWeekBackWarnsThatEveryStartRechecksItAll()
    {
        var logger = new CapturingLogger<CallLogCatchUp>();

        await CatchUp(new ReplayingHandler("report_summaries_page2") { Folder = "GoTo" }, new CallLogQueue(), new CallLogOptions { CatchUpFrom = Now.AddDays(-8) }, logger).RunAsync(Cancel);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal(
            $"CallLog:CatchUpFrom is {Shown(Now.AddDays(-8))}: every start re-checks every call since then. Remove it once a \"The call log catch-up queued N calls\" line is followed by a \"Call log queue empty\" line.",
            warning.Message);
    }

    [Fact]
    public async Task AStartLessThanAWeekBackDoesNotWarn()
    {
        var logger = new CapturingLogger<CallLogCatchUp>();

        await CatchUp(new ReplayingHandler("report_summaries_page2") { Folder = "GoTo" }, new CallLogQueue(), new CallLogOptions { CatchUpFrom = Now.AddDays(-6) }, logger).RunAsync(Cancel);

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    /// <summary>How the logger prints a time: the invariant culture's general format.</summary>
    private static string Shown(DateTimeOffset time) => time.ToString(CultureInfo.InvariantCulture);

    private static string ShownUtc(DateTimeOffset time) => time.UtcDateTime.ToString(CultureInfo.InvariantCulture);

    private static CallLogCatchUp CatchUp(HttpMessageHandler wire, CallLogQueue queue, CallLogOptions options, ILogger<CallLogCatchUp>? logger = null)
        => new(
            GoToTestServices.Build(wire, new NoWaitClock()).GetRequiredService<IServiceScopeFactory>(),
            queue,
            Options.Create(options),
            new FixedClock(Now),
            logger ?? NullLogger<CallLogCatchUp>.Instance);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
