using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
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
/// conversation 18 is with contact 20, already labelled <c>sales</c> with custom field
/// <c>visit</c>, and the one contact field is <c>probe_serial</c>.
/// </summary>
public sealed class RequestHumanToolTests
{
    private const string Chat = "cw_e86f9a8c-9b8c-4b84-9c3b-16b72acdbea1";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly IConversations _conversations = new Conversations(new InMemoryConversationStore(), blobs: null);
    private readonly CallbackOptions _callback = new();

    [Fact]
    public async Task TheTeamTheCallBackTheContactTheNoteAndTheStatusAreSetInOrder()
    {
        _callback.Promise = "within 2 hours, Mon-Fri 9-5";
        var wire = new ReplayingHandler(
        [
            ("conversation_shown", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
            ("conversation_attributes", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
            ("conversation_labels", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
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
                "GET conversations/18",
                "custom_attributes {\"custom_attributes\":{\"visit\":\"first\",\"callback_phone\":\"+12015550123\"}}",
                "GET labels",
                "labels {\"labels\":[\"sales\",\"callback\"]}",
                "contacts/20 email=dana@example.com",
                "custom_attribute_definitions",
                "contacts/20 {\"custom_attributes\":{\"probe_serial\":\"SN123\"}}",
                "messages note",
                "toggle_status {\"status\":\"open\"}",
            ],
            Steps(wire));

        Assert.Equal(
            "Call back\n- Phone: +1 201-555-0123\n- Email: dana@example.com (saved on the contact)\n\nWhat they want\n- A technician visit",
            NoteOf(wire));
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
        var options = Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            BotToken = "bot-token",
            ServiceToken = "service-token",
        });
        var client = new ChatwootClient(new HttpClient(wire), options);
        var tags = new ChatwootConversationTags(new HttpClient(wire), options);

        return new RequestHumanTool(
            _conversations,
            client,
            tags,
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
            var url when url.EndsWith("/custom_attributes", StringComparison.Ordinal) => "custom_attributes " + Plain(r.Body),
            var url when url.EndsWith("/labels", StringComparison.Ordinal) => r.Method == "GET" ? "GET labels" : "labels " + r.Body,
            var url when url.EndsWith("/messages", StringComparison.Ordinal) => "messages note",
            var url when url.Contains("custom_attribute_definitions", StringComparison.Ordinal) => "custom_attribute_definitions",
            _ when JsonNode.Parse(r.Body)!.AsObject() is var body && !body.ContainsKey("custom_attributes")
                => $"contacts/20 {body.Single().Key}={body.Single().Value!.GetValue<string>()}",
            _ => "contacts/20 " + r.Body,
        })];

    /// <summary>The JSON without escapes, such as <c>\u002B</c> for <c>+</c>, so it reads as written.</summary>
    private static string Plain(string json)
        => JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string NoteOf(ReplayingHandler wire)
    {
        var note = JsonNode.Parse(wire.Requests.Single(r => r.Url.EndsWith("/messages", StringComparison.Ordinal)).Body)!;

        Assert.True(note["private"]!.GetValue<bool>());

        return note["content"]!.GetValue<string>();
    }
}
