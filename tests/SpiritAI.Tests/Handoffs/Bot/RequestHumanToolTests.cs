using System.Net;
using System.Text.Json.Nodes;

using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Tests.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>
/// The handoff into Chatwoot (spec 9.3 and 9.5). Chatwoot's answers are a live Chatwoot's:
/// conversation 18 is with contact 20, the one contact field is <c>probe_serial</c>, and a phone
/// another contact has is refused with <c>422</c>.
/// </summary>
public sealed class RequestHumanToolTests
{
    private const string Chat = "cw_e86f9a8c-9b8c-4b84-9c3b-16b72acdbea1";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly IConversations _conversations = new Conversations(new InMemoryConversationStore(), blobs: null);
    private readonly CallbackOptions _callback = new();

    [Fact]
    public async Task TheTeamTheContactTheNoteAndTheStatusAreSetInOrder()
    {
        _callback.Promise = "within 2 hours, Mon-Fri 9-5";
        var wire = new ReplayingHandler(
        [
            ("conversation_shown", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
            ("contact_updated", HttpStatusCode.OK),
            ("contact_updated", HttpStatusCode.OK),
            ("contact_attribute_definitions", HttpStatusCode.OK),
            ("contact_updated", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
        ]);

        var answer = await AskAsync(wire, new HumanRequest(
            "(201) 555-0123",
            "Dana@Example.com",
            2,
            new Dictionary<string, string> { ["probe_serial"] = " SN123 ", ["not_a_field"] = "x" },
            "What they want\n- A technician visit"));

        Assert.Equal(
            "We will call you at +1 201-555-0123 within 2 hours, Mon-Fri 9-5. Your code is 18. If you call us first, give that code.",
            answer.Note);

        Assert.Equal(
            [
                "GET conversations/18",
                "assignments {\"team_id\":2}",
                "contacts/20 phone_number=+12015550123",
                "contacts/20 email=dana@example.com",
                "custom_attribute_definitions",
                "contacts/20 {\"custom_attributes\":{\"probe_serial\":\"SN123\"}}",
                "messages note",
                "toggle_status {\"status\":\"open\"}",
            ],
            Steps(wire));

        Assert.Equal(
            "Call back\n- Phone: +1 201-555-0123 (saved on the contact)\n- Email: dana@example.com (saved on the contact)\n\nWhat they want\n- A technician visit",
            NoteOf(wire));
    }

    [Fact]
    public async Task APhoneChatwootRefusesStaysInTheNoteAndTheCallIsStillPromised()
    {
        var wire = new ReplayingHandler(
        [
            ("conversation_shown", HttpStatusCode.OK),
            ("contact_phone_taken", HttpStatusCode.UnprocessableEntity),
            (null, HttpStatusCode.OK),
        ]);

        var answer = await AskAsync(wire, new HumanRequest("(201) 555-0123", Email: null, TeamId: null, ContactFields: null, "What they want\n- A call"));

        Assert.StartsWith("We will call you at +1 201-555-0123. Your code is 18.", answer.Note);
        Assert.Equal(
            "Call back\n- Phone: +1 201-555-0123 (not saved: Phone number has already been taken)\n- Email: none given\n\nWhat they want\n- A call",
            NoteOf(wire));
        Assert.EndsWith("toggle_status", wire.Requests[^1].Url);
    }

    [Fact]
    public async Task ANumberThatCannotBeReadIsNotSavedAndIsNamedInTheNote()
    {
        var wire = new ReplayingHandler(["conversation_shown", null]);

        var answer = await AskAsync(wire, new HumanRequest("12", Email: null, TeamId: null, ContactFields: null, "What they want\n- A call"));

        Assert.Equal(RequestHumanTool.NoPhoneNote + RequestHumanTool.BadPhoneNote + " Your code is 18.", answer.Note);
        Assert.DoesNotContain(wire.Requests, r => r.Url.Contains("/contacts/", StringComparison.Ordinal));
        Assert.StartsWith("Call back\n- Phone: 12 (not valid)\n", NoteOf(wire));
    }

    [Fact]
    public async Task AChatChatwootDoesNotHoldCannotBeHandedOff()
    {
        await _conversations.CreateAsync("signed-in-thread", Cancel);
        var wire = new ReplayingHandler("conversation_shown");

        var answer = await Tool(wire).AskAsync(
            "signed-in-thread", new HumanRequest("(201) 555-0123", null, null, null, "What they want\n- A call"), Cancel);

        Assert.Equal(RequestHumanTool.NotAChatwootChat, answer.Note);
        Assert.Empty(wire.Requests);
    }

    private async Task<RequestHumanAnswer> AskAsync(ReplayingHandler wire, HumanRequest request)
    {
        await _conversations.CreateAsync(Chat, Cancel);
        await _conversations.SetCustomAsync(Chat, new ChatwootIds(18, "probe-visitor-key-1").Write(custom: null), Cancel);

        return await Tool(wire).AskAsync(Chat, request, Cancel);
    }

    private RequestHumanTool Tool(ReplayingHandler wire)
    {
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            BotToken = "bot-token",
            ServiceToken = "service-token",
        }));

        return new RequestHumanTool(
            _conversations,
            client,
            new ListContactFieldsTool(client, TestHybridCache.Create()),
            Options.Create(_callback),
            NullLogger<RequestHumanTool>.Instance);
    }

    /// <summary>Each request, in a short form: the route's tail and what it sent.</summary>
    private static IReadOnlyList<string> Steps(ReplayingHandler wire)
        => [.. wire.Requests.Select(r => r.Url switch
        {
            var url when url.EndsWith("/conversations/18", StringComparison.Ordinal) => "GET conversations/18",
            var url when url.EndsWith("/assignments", StringComparison.Ordinal) => "assignments " + r.Body,
            var url when url.EndsWith("/toggle_status", StringComparison.Ordinal) => "toggle_status " + r.Body,
            var url when url.EndsWith("/messages", StringComparison.Ordinal) => "messages note",
            var url when url.Contains("custom_attribute_definitions", StringComparison.Ordinal) => "custom_attribute_definitions",
            _ when JsonNode.Parse(r.Body)!.AsObject() is var body && !body.ContainsKey("custom_attributes")
                => $"contacts/20 {body.Single().Key}={body.Single().Value!.GetValue<string>()}",
            _ => "contacts/20 " + r.Body,
        })];

    private static string NoteOf(ReplayingHandler wire)
    {
        var note = JsonNode.Parse(wire.Requests.Single(r => r.Url.EndsWith("/messages", StringComparison.Ordinal)).Body)!;

        Assert.True(note["private"]!.GetValue<bool>());

        return note["content"]!.GetValue<string>();
    }
}
