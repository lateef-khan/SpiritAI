using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Tests.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>
/// What the visitor's own contact already holds. <c>visitor_contact</c> is a live Chatwoot's:
/// contact 20 with a phone and no email.
/// </summary>
public sealed class KnownContactToolTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly IConversations _conversations = new Conversations(new InMemoryConversationStore(), blobs: null);

    [Fact]
    public async Task TheContactIsReadAsTheVisitorOfTheChat()
    {
        await _conversations.CreateAsync("cw_one", Cancel);
        await _conversations.SetCustomAsync("cw_one", new ChatwootIds(18, "probe-visitor-key-1").Write(custom: null), Cancel);
        var wire = new ReplayingHandler("visitor_contact");

        var answer = await Tool(wire).ReadAsync("cw_one", Cancel);

        Assert.Equal(new KnownContactAnswer("+13125550100", Email: null), answer);
        Assert.Equal("http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts/probe-visitor-key-1", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task AChatThatIsNotAChatwootOneKnowsNothing()
    {
        await _conversations.CreateAsync("signed-in-thread", Cancel);
        var wire = new ReplayingHandler("visitor_contact");

        var answer = await Tool(wire).ReadAsync("signed-in-thread", Cancel);

        Assert.Equal(new KnownContactAnswer(Phone: null, Email: null), answer);
        Assert.Empty(wire.Requests);
    }

    private KnownContactTool Tool(ReplayingHandler wire)
        => new(
            _conversations,
            new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions { BaseUrl = "http://chatwoot.test/", InboxIdentifier = "inbox-key" })));
}
