using System.Text.Json;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.RealTime;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>Which socket signals become the visitor's typing in Chatwoot.</summary>
public sealed class ChatwootTypingQueueTests
{
    private static readonly ChatwootOptions Configured = new()
    {
        BaseUrl = "http://chatwoot.test",
        AccountId = 2,
        InboxId = 1,
        InboxIdentifier = "inbox-key",
        BotToken = "bot-token",
    };

    private static RealTimeCaller Visitor(string chat)
        => new("visitor:abc", null, HandoffAdmission.VisitorKind, [HandoffGroups.ForConversation(chat), HandoffGroups.Visitors], _ => true);

    private static RealTimeSignal Typing(RealTimeCaller caller, string group, object payload)
        => new(new RealTimeSender(caller.Key, caller.Kind), group, "typing", JsonSerializer.SerializeToElement(payload));

    [Fact]
    public async Task TheVisitorsTypingIsKeptForTheChatTheyWereAdmittedTo()
    {
        using var queue = new ChatwootTypingQueue(Options.Create(Configured));
        var visitor = Visitor("chat-1");

        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { callId = "someone-else", on = true }));
        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { callId = "chat-1", on = false }));

        Assert.Equal([new VisitorTyping("chat-1", true), new VisitorTyping("chat-1", false)], await ReadAsync(queue, 2));
    }

    [Fact]
    public async Task AnythingElseIsNotKept()
    {
        using var queue = new ChatwootTypingQueue(Options.Create(Configured));
        var visitor = Visitor("chat-1");
        var staff = visitor with { Kind = HandoffAdmission.StaffKind };

        queue.Heard(staff, Typing(staff, HandoffGroups.Staff, new { on = true }));
        queue.Heard(visitor, Typing(visitor, "call:chat-2", new { on = true }));
        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { on = "yes" }));
        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { }));
        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { on = true }) with { Name = "waving" });
        queue.Heard(visitor with { Groups = [HandoffGroups.Visitors] }, Typing(visitor, HandoffGroups.Staff, new { on = true }));
        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { on = false }));

        Assert.Equal([new VisitorTyping("chat-1", false)], await ReadAsync(queue, 1));
    }

    [Fact]
    public async Task NothingIsKeptWhileTheCopyIsNotConfigured()
    {
        using var queue = new ChatwootTypingQueue(Options.Create(new ChatwootOptions()));
        var visitor = Visitor("chat-1");

        queue.Heard(visitor, Typing(visitor, HandoffGroups.Staff, new { on = true }));

        Assert.Empty(await ReadAsync(queue, 1));
    }

    [Fact]
    public async Task AFloodFromOneChatIsCutDownAndOtherChatsAreNot()
    {
        using var queue = new ChatwootTypingQueue(Options.Create(Configured));
        var flooder = Visitor("chat-1");
        var neighbour = Visitor("chat-2");

        for (var i = 0; i < 20; i++)
        {
            queue.Heard(flooder, Typing(flooder, HandoffGroups.Staff, new { on = i % 2 == 0 }));
        }

        queue.Heard(neighbour, Typing(neighbour, HandoffGroups.Staff, new { on = true }));

        var read = await ReadAsync(queue, 21);
        Assert.Equal(ChatwootTypingQueue.Burst, read.Count(t => t.ConversationId == "chat-1"));
        Assert.Equal(new VisitorTyping("chat-2", true), read[^1]);
    }

    /// <summary>Reads up to <paramref name="count"/> signals, or what is there after a short wait.</summary>
    private static async Task<List<VisitorTyping>> ReadAsync(ChatwootTypingQueue queue, int count)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        wait.CancelAfter(TimeSpan.FromMilliseconds(200));
        List<VisitorTyping> read = [];

        try
        {
            await foreach (var typing in queue.ReadAllAsync(wait.Token))
            {
                read.Add(typing);

                if (read.Count == count)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
        }

        return read;
    }
}
