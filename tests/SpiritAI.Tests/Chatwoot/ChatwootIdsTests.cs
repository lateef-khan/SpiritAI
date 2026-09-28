using System.Text.Json;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>The Chatwoot ids share AgentCore's <c>custom</c> column with the bookmark and anything else there.</summary>
public sealed class ChatwootIdsTests
{
    [Fact]
    public void FilingTheIdsKeepsTheBookmarkAndEveryOtherKey()
    {
        using var custom = JsonDocument.Parse("""{"owner":"visitor:abc","chatwoot":{"through":147}}""");

        var filed = new ChatwootIds(21, "livecheckkey3").Write(custom.RootElement);

        Assert.Equal(new ChatwootIds(21, "livecheckkey3"), ChatwootIds.Read(filed));
        Assert.Equal(147, ChatwootBookmark.Read(filed));
        Assert.Equal("visitor:abc", filed.GetProperty("owner").GetString());
    }

    [Fact]
    public void AConversationWithOnlyABookmarkHasNoIds()
    {
        using var custom = JsonDocument.Parse("""{"chatwoot":{"through":147}}""");

        Assert.Null(ChatwootIds.Read(custom.RootElement));
    }
}
