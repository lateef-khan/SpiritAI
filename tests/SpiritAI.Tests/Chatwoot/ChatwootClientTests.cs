using System.Net;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// The Chatwoot calls at the wire. The replies are the ones a live Chatwoot sent, kept in
/// <c>Payloads</c>.
/// </summary>
public sealed class ChatwootClientTests
{
    [Fact]
    public async Task AContactIsMadeByNameAndChatwootsOwnIdsComeBack()
    {
        var wire = new ReplayingHandler("contact_created");
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            InboxIdentifier = "inbox-key",
        }));

        var made = await client.CreateContactAsync("Visitor 7", TestContext.Current.CancellationToken);

        Assert.Equal(new ChatwootContact(10, "f33a4e94-b79d-4b64-986f-c3076d1c0109"), made);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts", request.Url);
        Assert.Equal("""{"name":"Visitor 7"}""", request.Body);
    }

    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public async Task TypingGoesAsTheContactThroughThePublicApi(bool on, string status)
    {
        var wire = new ReplayingHandler(payload: null);
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            InboxIdentifier = "inbox-key",
        }));

        await client.ToggleTypingAsync("f33a4e94-b79d-4b64-986f-c3076d1c0109", 42, on, TestContext.Current.CancellationToken);

        var request = Assert.Single(wire.Requests);
        Assert.Equal(
            "http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts/f33a4e94-b79d-4b64-986f-c3076d1c0109/conversations/42/toggle_typing",
            request.Url);
        Assert.Equal($$"""{"typing_status":"{{status}}"}""", request.Body);
    }

    [Fact]
    public async Task AgentsAreListedAsTheServiceUserWithTheirStatus()
    {
        var wire = new ReplayingHandler("agents");
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            BotToken = "bot-token",
            ServiceToken = "service-token",
        }));

        var agents = await client.ListAgentsAsync(TestContext.Current.CancellationToken);

        Assert.Equal([new ChatwootAgent(2, "online"), new ChatwootAgent(3, "offline")], agents);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/agents", request.Url);
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task TheBotReadsAConversationsIdsStatusAndContact()
    {
        var wire = new ReplayingHandler("conversation_shown");
        var client = Client(wire);

        var conversation = await client.GetConversationAsync(18, Cancel);

        Assert.Equal(new ChatwootConversation(18, Guid.Parse("e86f9a8c-9b8c-4b84-9c3b-16b72acdbea1"), "pending", 20), conversation);
        Assert.True(conversation.Pending);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/conversations/18", request.Url);
        Assert.Equal("bot-token", request.Token);
    }

    [Fact]
    public async Task TheBotGivesAConversationToATeam()
    {
        var wire = new ReplayingHandler(payload: null);

        await Client(wire).AssignTeamAsync(18, 2, Cancel);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/conversations/18/assignments", request.Url);
        Assert.Equal("""{"team_id":2}""", request.Body);
        Assert.Equal("bot-token", request.Token);
    }

    [Fact]
    public async Task TeamsAreListedAsTheServiceUser()
    {
        var wire = new ReplayingHandler("teams");

        var teams = await Client(wire).ListTeamsAsync(Cancel);

        Assert.Equal([new ChatwootTeam(2, "probe repairs", "Broken machines and parts")], teams);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/teams", request.Url);
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task ContactFieldsAreListedAsTheServiceUser()
    {
        var wire = new ReplayingHandler("contact_attribute_definitions");

        var fields = await Client(wire).ListContactFieldsAsync(Cancel);

        Assert.Equal([new ChatwootContactField("probe_serial", "Probe Serial", "text", "The serial number on the frame")], fields);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/custom_attribute_definitions?attribute_model=1", request.Url);
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task AContactFieldIsSetThroughTheStaffApi()
    {
        var wire = new ReplayingHandler("contact_updated");

        var update = await Client(wire).UpdateContactFieldAsync(20, "phone_number", "+13125550100", Cancel);

        Assert.True(update.WasSaved);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/contacts/20", request.Url);
        Assert.Equal("+13125550100", JsonNode.Parse(request.Body)!["phone_number"]!.GetValue<string>());
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task APhoneAnotherContactHasIsRefusedWithChatwootsReason()
    {
        var wire = new ReplayingHandler("contact_phone_taken", HttpStatusCode.UnprocessableEntity);

        var update = await Client(wire).UpdateContactFieldAsync(21, "phone_number", "+13125550100", Cancel);

        Assert.False(update.WasSaved);
        Assert.Equal("Phone number has already been taken", update.Refusal);
    }

    [Fact]
    public async Task CustomFieldsAreSetThroughTheStaffApi()
    {
        var wire = new ReplayingHandler("contact_updated");

        var update = await Client(wire).UpdateContactAttributesAsync(
            20, new Dictionary<string, string> { ["probe_serial"] = "SN123" }, Cancel);

        Assert.True(update.WasSaved);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/contacts/20", request.Url);
        Assert.Equal("""{"custom_attributes":{"probe_serial":"SN123"}}""", request.Body);
        Assert.Equal("service-token", request.Token);
    }

    [Fact]
    public async Task APageOfMessagesIsReadAsTheVisitorOldestFirst()
    {
        var wire = new ReplayingHandler("visitor_messages");

        var messages = await Client(wire).ListMessagesAsync("probe-visitor-key-1", 18, before: null, Cancel);

        Assert.Equal(
            [
                new ChatwootMessage(95, "My treadmill belt slips.", 0, "contact", "Probe Visitor"),
                new ChatwootMessage(96, "Sorry to hear that. **Which model** is it?", 1, "agent_bot", "Spirit AI"),
                new ChatwootMessage(98, "Hi, this is Matthew from support.", 1, "user", "Matthew Hsu"),
            ],
            messages);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts/probe-visitor-key-1/conversations/18/messages", request.Url);
        Assert.Null(request.Token);
    }

    [Fact]
    public async Task AnOlderPageIsAskedForWithBefore()
    {
        var wire = new ReplayingHandler("visitor_messages");

        await Client(wire).ListMessagesAsync("probe-visitor-key-1", 18, before: 96, Cancel);

        Assert.EndsWith("/conversations/18/messages?before=96", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task AConversationThatIsNotTheVisitorsHasNoMessages()
    {
        var wire = new ReplayingHandler(payload: null, HttpStatusCode.NotFound);

        Assert.Null(await Client(wire).ListMessagesAsync("probe-visitor-key-2", 18, before: null, Cancel));
    }

    [Fact]
    public async Task TheVisitorsOwnContactIsReadAsTheVisitor()
    {
        var wire = new ReplayingHandler("visitor_contact");

        var contact = await Client(wire).GetVisitorContactAsync("probe-visitor-key-1", Cancel);

        Assert.Equal(new ChatwootVisitorContact(20, Email: null, "+13125550100"), contact);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts/probe-visitor-key-1", request.Url);
        Assert.Null(request.Token);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static ChatwootClient Client(ReplayingHandler wire) => new(new HttpClient(wire), Options.Create(new ChatwootOptions
    {
        BaseUrl = "http://chatwoot.test/",
        AccountId = 2,
        InboxIdentifier = "inbox-key",
        BotToken = "bot-token",
        ServiceToken = "service-token",
    }));
}
