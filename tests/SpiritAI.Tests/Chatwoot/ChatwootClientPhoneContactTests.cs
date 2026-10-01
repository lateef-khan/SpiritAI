using System.Net;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// The phone-contact calls at the wire. The replies are the ones local Chatwoot sent in Task 1, with
/// the contact as 7, its source id as <c>5f0c6c2e-…0007</c>, and the conversation as 31.
/// </summary>
public sealed class ChatwootClientPhoneContactTests
{
    private const string Account = "http://chatwoot.test/api/v1/accounts/2";

    private const string SourceId = "5f0c6c2e-0000-4000-8000-000000000007";

    private const string CallSource = "goto:84dcee04-ba4d-33c5-b00b-c0e57599fa69";

    private const string CallMarker = "GoTo call 84dcee04-ba4d-33c5-b00b-c0e57599fa69";

    private static readonly DateTimeOffset LongAgo = DateTimeOffset.UnixEpoch;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AContactIsFoundByItsExactNumberWithItsInboxKey()
    {
        var wire = new ReplayingHandler("phone_contacts_found");

        Assert.Equal(new ChatwootPhoneContact(7, SourceId), await Client(wire).FindPhoneContactAsync("+12015550100", Cancel));

        var request = Assert.Single(wire.Requests);
        Assert.Equal($"{Account}/contacts/search?q=%2B12015550100", request.Url);
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task AContactWithNoPlaceInTheInboxHasNoKey()
        => Assert.Equal(new ChatwootPhoneContact(7, null), await Client(new ReplayingHandler("phone_contacts_found_no_inbox")).FindPhoneContactAsync("+12015550100", Cancel));

    [Fact]
    public async Task NoContactHasTheNumber()
        => Assert.Null(await Client(new ReplayingHandler("contacts_none")).FindPhoneContactAsync("+12015550100", Cancel));

    [Fact]
    public async Task AContactIsCreatedInTheInboxAsTheServiceUser()
    {
        var wire = new ReplayingHandler("phone_contact_created");

        Assert.Equal(new ChatwootPhoneContact(7, SourceId), await Client(wire).CreatePhoneContactAsync("+1 201-555-0100", "+12015550100", Cancel));

        var request = Assert.Single(wire.Requests);
        Assert.Equal(("POST", $"{Account}/contacts"), (request.Method, request.Url));
        Assert.Equal("""{"name":"\u002B1 201-555-0100","phone_number":"\u002B12015550100","inbox_id":1}""", request.Body);
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task ANumberTakenAMomentAgoGivesTheContactThatHasIt()
    {
        var wire = new ReplayingHandler([("contact_phone_taken", HttpStatusCode.UnprocessableEntity), ("phone_contacts_found", HttpStatusCode.OK)]);

        Assert.Equal(new ChatwootPhoneContact(7, SourceId), await Client(wire).CreatePhoneContactAsync("+1 201-555-0100", "+12015550100", Cancel));

        Assert.Equal(["POST", "GET"], wire.Requests.Select(r => r.Method));
        Assert.Equal($"{Account}/contacts/search?q=%2B12015550100", wire.Requests[1].Url);
    }

    [Fact]
    public async Task ARefusalWithNoContactBehindItIsThrownWithChatwootsWords()
    {
        var wire = new ReplayingHandler([("contact_phone_taken", HttpStatusCode.UnprocessableEntity), ("contacts_none", HttpStatusCode.OK)]);

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(wire).CreatePhoneContactAsync("+1 201-555-0100", "+12015550100", Cancel));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, thrown.StatusCode);
        Assert.Contains("Phone number has already been taken", thrown.Message);
        Assert.DoesNotContain("201", thrown.Message);
    }

    [Fact]
    public async Task AContactIsAddedToTheInbox()
    {
        var wire = new ReplayingHandler("phone_contact_inbox_created");

        Assert.Equal(SourceId, await Client(wire).AddContactToInboxAsync(7, Cancel));

        var request = Assert.Single(wire.Requests);
        Assert.Equal(("POST", $"{Account}/contacts/7/contact_inboxes"), (request.Method, request.Url));
        Assert.Equal("""{"inbox_id":1}""", request.Body);
    }

    [Fact]
    public async Task TheContactsConversationInTheInboxIsFound()
    {
        var wire = new ReplayingHandler("phone_contact_conversations");

        Assert.Equal(31, await Client(wire).FindInboxConversationAsync(7, Cancel));
        Assert.Equal($"{Account}/contacts/7/conversations", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task AContactWithNoConversationHasNone()
        => Assert.Null(await Client(new ReplayingHandler("phone_contact_conversations_none")).FindInboxConversationAsync(7, Cancel));

    [Fact]
    public async Task AConversationIsCreatedInTheInboxAsTheServiceUser()
    {
        var wire = new ReplayingHandler("phone_conversation_created");

        Assert.Equal(new ChatwootCreatedConversation(31, Fresh: true), await Client(wire).CreateConversationAsync(7, SourceId, Cancel));

        var create = Assert.Single(wire.Requests);
        Assert.Equal(("POST", $"{Account}/conversations"), (create.Method, create.Url));
        Assert.Equal($$"""{"source_id":"{{SourceId}}","inbox_id":1,"contact_id":7}""", create.Body);
        Assert.Equal("service-token", create.Token);
    }

    [Fact]
    public async Task ACreateAnswerHoldingAMessageIsAnExistingConversation()
    {
        var wire = new AnsweringHandler(ChatwootPayloads.ConversationCreatedWithAMessage());

        Assert.Equal(new ChatwootCreatedConversation(31, Fresh: false), await Client(wire).CreateConversationAsync(7, SourceId, Cancel));
    }

    [Fact]
    public async Task AConversationIsResolvedAsTheServiceUser()
    {
        var wire = new ReplayingHandler("phone_conversation_resolved");

        await Client(wire).ResolveConversationAsync(31, Cancel);

        var resolve = Assert.Single(wire.Requests);
        Assert.Equal(("POST", $"{Account}/conversations/31/toggle_status"), (resolve.Method, resolve.Url));
        Assert.Equal("""{"status":"resolved"}""", resolve.Body);
        Assert.Equal("service-token", resolve.Token);
    }

    [Fact]
    public async Task ANoteIsFoundByItsSourceId()
        => Assert.True(await Client(new ReplayingHandler("phone_conversation_messages")).HasNoteAsync(31, CallSource, CallMarker, LongAgo, Cancel));

    [Fact]
    public async Task ANoteIsFoundByItsLastLineWhenChatwootDroppedTheSourceId()
        => Assert.True(await Client(new ReplayingHandler("phone_conversation_messages_marker_only")).HasNoteAsync(31, CallSource, CallMarker, LongAgo, Cancel));

    [Fact]
    public async Task AnEmptyConversationHasNoNote()
    {
        var wire = new ReplayingHandler("phone_conversation_messages_empty");

        Assert.False(await Client(wire).HasNoteAsync(31, CallSource, CallMarker, LongAgo, Cancel));
        Assert.Equal($"{Account}/conversations/31/messages", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task ANoteOnAnOlderPageIsFound()
    {
        var wire = new ReplayingHandler(["phone_conversation_messages_full", "phone_conversation_messages_older"]);

        Assert.True(await Client(wire).HasNoteAsync(31, CallSource, CallMarker, LongAgo, Cancel));

        // 2 is the smallest message id in phone_conversation_messages_full.json.
        Assert.Equal($"{Account}/conversations/31/messages?before=2", wire.Requests[1].Url);
    }

    [Fact]
    public async Task PagesOlderThanTheCallAreNotRead()
    {
        var wire = new ReplayingHandler(["phone_conversation_messages_full", "phone_conversation_messages_older"]);

        // phone_conversation_messages_full.json reaches back to 1790836360, one second before the call.
        var call = DateTimeOffset.FromUnixTimeSeconds(1790836361);

        Assert.False(await Client(wire).HasNoteAsync(31, CallSource, CallMarker, call, Cancel));
        Assert.Single(wire.Requests);
    }

    [Fact]
    public async Task ACallNoteTheSearchFindsInTheInboxIsThere()
    {
        var wire = new ReplayingHandler("message_search_found");

        Assert.True(await Client(wire).HasCallNoteAsync(CallMarker, Cancel));

        var request = Assert.Single(wire.Requests);
        Assert.Equal(("GET", $"{Account}/search/messages?q=GoTo call 84dcee04-ba4d-33c5-b00b-c0e57599fa69"), (request.Method, request.Url));
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task ACallTheSearchDoesNotFindHasNoNote()
        => Assert.False(await Client(new ReplayingHandler("message_search_none")).HasCallNoteAsync("GoTo call 00000000-0000-0000-0000-000000000000", Cancel));

    [Fact]
    public async Task ANoteInAnotherInboxIsNotTheCallsNote()
        => Assert.False(await Client(new ReplayingHandler("message_search_found"), inboxId: 2).HasCallNoteAsync(CallMarker, Cancel));

    [Fact]
    public async Task ANoteIsPostedPrivatelyAsTheBotWithItsSourceId()
    {
        var wire = new ReplayingHandler(payload: null);

        await Client(wire).PostNoteAsync(31, "probe note", CallSource, Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal(("POST", $"{Account}/conversations/31/messages"), (request.Method, request.Url));
        Assert.Equal($$"""{"content":"probe note","message_type":"outgoing","private":true,"source_id":"{{CallSource}}"}""", request.Body);
        Assert.Equal("bot-token", request.Token);
    }

    [Fact]
    public async Task ANoteWithNoSourceIdCarriesNone()
    {
        var wire = new ReplayingHandler(payload: null);

        await Client(wire).PostNoteAsync(31, "probe note", sourceId: null, Cancel);

        Assert.Equal("""{"content":"probe note","message_type":"outgoing","private":true}""", Assert.Single(wire.Requests).Body);
    }

    [Fact]
    public async Task APartOfANumberIsNotAContact()
        => Assert.Null(await Client(new ReplayingHandler("contacts_found")).FindPhoneContactAsync("+1201555012", Cancel));

    private static ChatwootClient Client(HttpMessageHandler wire, int inboxId = 1)
        => new(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            InboxId = inboxId,
            InboxIdentifier = "inbox-key",
            BotToken = "bot-token",
            ServiceToken = "service-token",
        }));
}
