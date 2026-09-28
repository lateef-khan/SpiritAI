using System.Text.Json;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>The bookmark lives in AgentCore's <c>custom</c> column, next to whatever else is there.</summary>
public sealed class ChatwootBookmarkTests
{
    [Fact]
    public void MovingTheBookmarkKeepsEveryOtherKey()
    {
        using var custom = JsonDocument.Parse("""{"owner":"visitor:abc","chatwoot":{"through":146,"conversation":19}}""");

        var moved = ChatwootBookmark.Write(custom.RootElement, 147);

        Assert.Equal(147, ChatwootBookmark.Read(moved));
        Assert.Equal("visitor:abc", moved.GetProperty("owner").GetString());
        Assert.Equal(19, moved.GetProperty("chatwoot").GetProperty("conversation").GetInt32());
    }

    [Fact]
    public void ACopyWithNoCustomColumnHasNoBookmark()
    {
        Assert.Null(ChatwootBookmark.Read(custom: null));
    }
}
