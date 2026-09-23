using System.Security.Claims;

using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using SpiritAI.Contacts;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.PublicChat;
using SpiritAI.RealTime;
using SpiritAI.Tests.Auth;
using SpiritAI.Tests.Contacts;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Handoffs.RealTime;

/// <summary>
/// Who may join the handoff socket, and what they may say. Only a visitor who owns the chat joins;
/// staff work in Chatwoot.
/// </summary>
public sealed class HandoffAdmissionTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly InMemoryConversationStore _conversations = new(new TestTimeProvider(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero)));

    private readonly FakeContactResolver _contacts = new();

    private readonly FakeContactConversationStore _contactConversations = new();

    private readonly HandoffAdmission _admission;

    public HandoffAdmissionTests()
    {
        _admission = new HandoffAdmission(new Conversations(_conversations, blobs: null), _contacts, _contactConversations);
    }

    [Fact]
    public async Task ASignedInCallerIsNobodyToThisFeature()
    {
        var caller = await _admission.AdmitAsync(new RealTimeRequest(SignedIn("user_dana", "dana@example.com"), Query()), Cancel);

        Assert.Null(caller);
    }

    [Fact]
    public async Task AVisitorJoinsItsOwnChatAndMaySignalStaff()
    {
        var conversationId = await MakeVisitorChatAsync("v1");

        var caller = await _admission.AdmitAsync(new RealTimeRequest(null, Query(("call", conversationId), ("visitor", "v1"))), Cancel);

        Assert.NotNull(caller);
        Assert.Equal(VisitorPrincipal.KeyOf("v1"), caller.Key);
        Assert.Null(caller.Name);
        Assert.Equal(HandoffAdmission.VisitorKind, caller.Kind);
        Assert.Equal([HandoffGroups.ForConversation(conversationId), HandoffGroups.Visitors], caller.Groups);
        Assert.True(caller.MaySignal(HandoffGroups.Staff));
        Assert.False(caller.MaySignal(HandoffGroups.ForConversation(conversationId)));
        Assert.False(caller.MaySignal("call:x"));
    }

    [Fact]
    public async Task AVisitorWithTheWrongKeyIsNobody()
    {
        var conversationId = await MakeVisitorChatAsync("v1");

        Assert.Null(await _admission.AdmitAsync(new RealTimeRequest(null, Query(("call", conversationId), ("visitor", "v2"))), Cancel));
        Assert.Null(await _admission.AdmitAsync(new RealTimeRequest(null, Query(("call", "no-such-conversation"), ("visitor", "v1"))), Cancel));
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task AMalformedKeyIsNobody(string visitor)
    {
        var conversationId = await MakeVisitorChatAsync("v1");

        Assert.Null(await _admission.AdmitAsync(new RealTimeRequest(null, Query(("call", conversationId), ("visitor", visitor))), Cancel));
    }

    [Fact]
    public async Task NoQueryIsNobody()
    {
        Assert.Null(await _admission.AdmitAsync(new RealTimeRequest(null, Query()), Cancel));
        Assert.Null(await _admission.AdmitAsync(new RealTimeRequest(null, Query(("visitor", "v1"))), Cancel));
    }

    private async Task<string> MakeVisitorChatAsync(string visitorKey)
    {
        var conversationId = Guid.NewGuid().ToString("N");
        var key = VisitorPrincipal.KeyOf(visitorKey);

        await _conversations.CreateAsync(conversationId, Cancel);
        await _conversations.SetCustomAsync(conversationId, ThreadEnvelope.Build(key, null), Cancel);

        var contactId = await _contacts.ResolveAsync(key, Cancel);
        await _contactConversations.EnsureAsync(conversationId, contactId, ContactChannel.Chat, Cancel);

        return conversationId;
    }

    private static ClaimsPrincipal SignedIn(string subject, string email)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject), new Claim(ClaimTypes.Email, email)],
            authenticationType: "test"));

    private static QueryCollection Query(params (string Name, string Value)[] fields)
        => new(fields.ToDictionary(field => field.Name, field => new StringValues(field.Value)));
}
