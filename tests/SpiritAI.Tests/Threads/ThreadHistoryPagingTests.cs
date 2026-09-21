using System.Net;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>A thread's words come down a page of turns at a time, newest page first.</summary>
public sealed class ThreadHistoryPagingTests
{
    [Fact]
    public async Task TheNewestPageComesFirst_AndNamesTheCursorForTheOlderOne()
    {
        await using var world = await ThreadWorld.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();
        await world.TurnAsync(remoteId, 0, "one", "first");
        await world.TurnAsync(remoteId, 1, "two", "second");
        await world.TurnAsync(remoteId, 2, "three", "third");

        var page = await world.Owner.HistoryAsync(remoteId, "?limit=2");

        Assert.Equal(["two", "second", "three", "third"], page.Messages.Select(item => TextOf(item.Message)));
        Assert.Equal(page.Messages[^1].Message.Id, page.HeadId);
        Assert.Equal("1", page.NextCursor);
    }

    [Fact]
    public async Task TheCursorReadsTheOlderPage_WhichNamesNoneAtTheStart()
    {
        await using var world = await ThreadWorld.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();
        await world.TurnAsync(remoteId, 0, "one", "first");
        await world.TurnAsync(remoteId, 1, "two", "second");
        await world.TurnAsync(remoteId, 2, "three", "third");

        var page = await world.Owner.HistoryAsync(remoteId, "?before=1&limit=2");

        Assert.Equal(["one", "first"], page.Messages.Select(item => TextOf(item.Message)));
        Assert.Null(page.Messages[0].ParentId);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ACursorTheHostNeverWrote_IsRefused()
    {
        await using var world = await ThreadWorld.StartAsync();
        var remoteId = await world.Owner.CreateThreadAsync();

        var response = await world.Owner.GetAsync($"{ThreadWorld.Threads}/{remoteId}/messages?before=yesterday");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string TextOf(SpiritAI.Threads.ThreadHistoryMessage message)
        => string.Concat(message.Content.OfType<SpiritAI.Threads.ThreadTextPart>().Select(part => part.Text));
}
