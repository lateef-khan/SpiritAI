using SpiritAI.Handoffs.Store;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Store;

public sealed class HandoffCursorTests
{
    [Fact]
    public void ACursorReadsBackAsItWasWritten()
    {
        var cursor = new HandoffCursor(new DateTimeOffset(2026, 9, 19, 14, 30, 5, TimeSpan.FromHours(8)), 42);

        Assert.True(HandoffCursor.TryParse(cursor.Encode(), out var read));
        Assert.Equal(cursor.At, read.At);
        Assert.Equal(cursor.Id, read.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("MTIz")]
    [InlineData("YTpi")]
    [InlineData("LTE6MQ")]
    public void TextThisHostDidNotWriteIsRefused(string text)
    {
        Assert.False(HandoffCursor.TryParse(text, out _));
    }
}
