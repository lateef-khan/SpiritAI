using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>
/// Whether the office is open, from the Spirit inbox's hours. <c>inbox_hours</c> is a live
/// Chatwoot's: <c>America/Chicago</c>, Monday to Friday 09:00 to 17:00, the weekend closed.
/// Chicago is 5 hours behind UTC in September. 2026-09-24 is a Thursday.
/// </summary>
public sealed class BusinessHoursToolTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InsideTheHoursTheOfficeIsOpen()
    {
        var answer = await ReadAtAsync("inbox_hours", new DateTimeOffset(2026, 9, 24, 19, 5, 0, TimeSpan.Zero));

        Assert.Equal(new BusinessHoursAnswer(true, "America/Chicago", "Thursday 14:05", "09:00-17:00", NextOpening: null), answer);
    }

    [Fact]
    public async Task BeforeTheHoursTheOfficeOpensLaterToday()
    {
        var answer = await ReadAtAsync("inbox_hours", new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new BusinessHoursAnswer(false, "America/Chicago", "Friday 07:00", "09:00-17:00", "Friday 2026-09-25 09:00"), answer);
    }

    [Fact]
    public async Task AfterTheHoursTheOfficeOpensTomorrow()
    {
        var answer = await ReadAtAsync("inbox_hours", new DateTimeOffset(2026, 9, 24, 23, 0, 0, TimeSpan.Zero));

        Assert.Equal(new BusinessHoursAnswer(false, "America/Chicago", "Thursday 18:00", "09:00-17:00", "Friday 2026-09-25 09:00"), answer);
    }

    [Fact]
    public async Task OnTheWeekendTheOfficeOpensOnMonday()
    {
        var answer = await ReadAtAsync("inbox_hours", new DateTimeOffset(2026, 9, 26, 17, 0, 0, TimeSpan.Zero));

        Assert.Equal(new BusinessHoursAnswer(false, "America/Chicago", "Saturday 12:00", "closed", "Monday 2026-09-28 09:00"), answer);
    }

    [Fact]
    public async Task AnInboxWithNoHoursIsAlwaysOpen()
    {
        var answer = await ReadAtAsync("inbox_no_hours", new DateTimeOffset(2026, 9, 26, 17, 0, 0, TimeSpan.Zero));

        Assert.Equal(new BusinessHoursAnswer(true, "UTC", "Saturday 17:00", "no hours set", NextOpening: null), answer);
    }

    [Fact]
    public async Task TheHoursAreReadFromChatwootOnceAndKept()
    {
        var wire = new ReplayingHandler("inbox_hours");
        var tool = Tool(wire, new DateTimeOffset(2026, 9, 24, 19, 5, 0, TimeSpan.Zero));

        await tool.ReadAsync(Cancel);
        await tool.ReadAsync(Cancel);

        Assert.Equal("http://chatwoot.test/public/api/v1/inboxes/inbox-key", Assert.Single(wire.Requests).Url);
    }

    private static Task<BusinessHoursAnswer> ReadAtAsync(string inbox, DateTimeOffset now)
        => Tool(new ReplayingHandler(inbox), now).ReadAsync(Cancel);

    private static BusinessHoursTool Tool(ReplayingHandler wire, DateTimeOffset now)
        => new(
            new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions { BaseUrl = "http://chatwoot.test/", InboxIdentifier = "inbox-key" })),
            TestHybridCache.Create(),
            new TestTimeProvider(now));
}
