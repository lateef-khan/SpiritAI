using AgentCore.Application.Transcript;

using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>How a page of a thread's words is spelled on the query string, and read back off it.</summary>
public sealed class HistoryWindowTests
{
    [Fact]
    public void NoQuery_IsTheNewestPage_AtTheDefaultSize()
    {
        Assert.True(HistoryWindow.TryRead(new HistoryQuery(Before: null, Limit: null), out var window));

        Assert.Equal(new TranscriptWindow(null, 30), window);
    }

    [Fact]
    public void ACursorAndALimit_ReadBackAsTheyWereSent()
    {
        Assert.True(HistoryWindow.TryRead(new HistoryQuery("7", 5), out var window));

        Assert.Equal(new TranscriptWindow(7, 5), window);
    }

    [Theory]
    [InlineData(500, 100)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    public void ALimitOutsideTheRange_IsClamped(int asked, int given)
    {
        Assert.True(HistoryWindow.TryRead(new HistoryQuery(Before: null, asked), out var window));

        Assert.Equal(given, window.Turns);
    }

    [Theory]
    [InlineData("seven")]
    [InlineData("-1")]
    [InlineData("")]
    public void ACursorThisHostNeverWrote_IsRefused(string before)
    {
        Assert.False(HistoryWindow.TryRead(new HistoryQuery(before, Limit: null), out _));
    }

    [Fact]
    public void TheCursorIsTheTurnAsText_AndAbsentAtTheStart()
    {
        Assert.Equal("7", HistoryWindow.CursorOf(7));
        Assert.Null(HistoryWindow.CursorOf(null));
    }
}
