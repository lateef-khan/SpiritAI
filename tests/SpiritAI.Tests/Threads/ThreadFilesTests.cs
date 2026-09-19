using AgentCore.Application.Blobs;
using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// The files a stored reply points at, and how they come back to the browser.
/// </summary>
public sealed class ThreadFilesTests
{
    private static readonly DateTimeOffset Made = DateTimeOffset.Parse("2026-09-17T09:00:00Z", null);

    private static readonly ConversationRecord Conversation =
        new("conversation-1", null, ConversationStatus.Regular, null, null, Made, null);

    private static readonly Uri Link = new("https://files.test/conversation-1/chart.png?t=1");

    /// <summary>A stored reply: one file the capture kept, one it refused.</summary>
    private static ChatMessage ReplyWith(string words, string keptName, string refusedName)
    {
        FileContent kept = new() { Name = keptName, FileId = "cfile_1", MediaType = "image/png", Length = 10, Kept = true };
        FileContent refused = new() { Name = refusedName, FileId = "cfile_2" };

        return new ChatMessage(ChatRole.Assistant, [new TextContent(words), kept, refused]);
    }

    [Fact]
    public void PartOf_CarriesTheFactsAndTheLink_AndDecidesNothing()
    {
        var part = ThreadFiles.PartOf(new BlobRef("conversation-1", "chart.png", "image/png", 10), Link);

        Assert.Equal("chart.png", part.Name);
        Assert.Equal("image/png", part.MediaType);
        Assert.Equal(10, part.Length);
        Assert.Equal(Link.ToString(), part.Url);
    }

    [Fact]
    public void PartsOf_KeysEachLinkedFileByName_AndSkipsOneWithNoLink()
    {
        var parts = ThreadFiles.PartsOf(
        [
            new FileLink(new BlobRef("conversation-1", "chart.png", "image/png", 10), Link),
            new FileLink(new BlobRef("conversation-1", "rows.csv", "text/csv", 8), null),
        ]);

        var (name, part) = Assert.Single(parts);
        Assert.Equal("chart.png", name);
        Assert.Equal("chart.png", Assert.IsType<ThreadFilePart>(part).Name);
    }

    [Fact]
    public async Task Of_ALoadedConversation_PutsTheLinkedFileAfterTheWords_AndLeavesTheSandboxLinkInThem()
    {
        StubBlobStore blobs = new();
        IConversations conversations = new Conversations(new InMemoryConversationStore(), blobs);
        var token = TestContext.Current.CancellationToken;
        await conversations.CreateAsync("conversation-1", token);
        await conversations.AppendMessageAsync("conversation-1", ReplyWith("See [chart](sandbox:/mnt/data/chart.png)", "chart.png", "lost.csv"), token);

        var stored = await conversations.LoadAsync("conversation-1", token);
        var history = ThreadHistory.Of(stored!);

        var content = Assert.Single(history.Messages).Message.Content;
        Assert.Collection(
            content,
            part => Assert.Equal("See [chart](sandbox:/mnt/data/chart.png)", Assert.IsType<ThreadTextPart>(part).Text),
            part => Assert.Equal("chart.png", Assert.IsType<ThreadFilePart>(part).Name));
    }

    private sealed class StubBlobStore : IBlobStore
    {
        public bool Links { get; init; } = true;

        public ValueTask<BlobRef> PutAsync(BlobWrite write, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<BlobRead?> OpenReadAsync(string ownerId, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<BlobRef?> StatAsync(string ownerId, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The read path links from the stamp and never asks the store.");

        public ValueTask<Uri?> LinkAsync(BlobRef blob, TimeSpan lifetime, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Links ? new Uri($"https://files.test/{blob.OwnerId}/{blob.Name}?t=1") : null);

        public ValueTask DeleteByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
