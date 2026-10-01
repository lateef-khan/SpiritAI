using System.Net;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.CallLog;
using SpiritAI.Chatwoot;
using SpiritAI.GoTo;
using SpiritAI.Tests.Chatwoot;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.CallLog;

/// <summary>
/// One call copied from GoTo's recorded report into a replayed Chatwoot. The Chatwoot replies are
/// Task 1's, with the contact as 7 and the conversation as 31.
/// </summary>
public sealed class CallLogCopierTests : IAsyncDisposable
{
    private const string Account = "http://chatwoot.test/api/v1/accounts/2";

    private const string CallId = "84dcee04-ba4d-33c5-b00b-c0e57599fa69";

    private const string ReportUrl = $"https://api.goto.com/call-events-report/v1/reports/{CallId}";

    private static readonly string AnsweredNote = """
        📞 Inbound call · Answered by Test Person 8625 (8625)
        Line: Spirit +1 800-258-8511 → Service - Premium
        Caller ID: PROBE CALLER, +1 201-555-0100
        Thu Sep 24, 2026, 12:47 PM CDT · waited 0:44 · talked 6:14
        GoTo call 84dcee04-ba4d-33c5-b00b-c0e57599fa69
        """.ReplaceLineEndings("\n");

    private readonly ReplayingHandler _goto = new(["report_answered", "voice_extensions", "voice_phone_numbers"]) { Folder = "GoTo" };

    private readonly ServiceProvider _gotoServices;

    public CallLogCopierTests() => _gotoServices = GoToTestServices.Build(_goto);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ANewNumberGetsAContactAResolvedConversationAndTheNote()
    {
        var chatwoot = new ReplayingHandler(
            ["message_search_none", "contacts_none", "phone_contact_created", "phone_contact_conversations_none", "phone_conversation_created", "phone_conversation_resolved", "phone_conversation_messages_empty", null]);

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        AssertGoToWasRead();

        Assert.Equal(
            [
                $"GET {Account}/search/messages?q=GoTo call 84dcee04-ba4d-33c5-b00b-c0e57599fa69",
                $"GET {Account}/contacts/search?q=%2B12015550100",
                $"POST {Account}/contacts",
                $"GET {Account}/contacts/7/conversations",
                $"POST {Account}/conversations",
                $"POST {Account}/conversations/31/toggle_status",
                $"GET {Account}/conversations/31/messages",
                $"POST {Account}/conversations/31/messages",
            ],
            chatwoot.Requests.Select(r => $"{r.Method} {r.Url}"));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""{"name":"+1 201-555-0100","phone_number":"+12015550100","inbox_id":1}"""),
            JsonNode.Parse(chatwoot.Requests[2].Body)));

        var note = JsonNode.Parse(chatwoot.Requests[^1].Body)!;
        Assert.Equal(AnsweredNote, note["content"]!.GetValue<string>());
        Assert.Equal($"goto:{CallId}", note["source_id"]!.GetValue<string>());
        Assert.True(note["private"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AResolveThatFailsOnceIsTriedAgainAndTheNoteIsPosted()
    {
        var chatwoot = new ReplayingHandler(
        [
            ("message_search_none", HttpStatusCode.OK),
            ("contacts_none", HttpStatusCode.OK),
            ("phone_contact_created", HttpStatusCode.OK),
            ("phone_contact_conversations_none", HttpStatusCode.OK),
            ("phone_conversation_created", HttpStatusCode.OK),
            (null, HttpStatusCode.InternalServerError),
            ("phone_conversation_resolved", HttpStatusCode.OK),
            ("phone_conversation_messages_empty", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
        ]);

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        Assert.Equal(2, chatwoot.Requests.Count(r => r.Url == $"{Account}/conversations/31/toggle_status"));
        Assert.Equal($"POST {Account}/conversations/31/messages", $"{chatwoot.Requests[^1].Method} {chatwoot.Requests[^1].Url}");
    }

    [Fact]
    public async Task AResolveThatNeverWorksStillPostsTheNoteAndWarnsWithTheIdsOnly()
    {
        var chatwoot = new ReplayingHandler(
        [
            ("message_search_none", HttpStatusCode.OK),
            ("contacts_none", HttpStatusCode.OK),
            ("phone_contact_created", HttpStatusCode.OK),
            ("phone_contact_conversations_none", HttpStatusCode.OK),
            ("phone_conversation_created", HttpStatusCode.OK),
            (null, HttpStatusCode.InternalServerError),
            (null, HttpStatusCode.InternalServerError),
            (null, HttpStatusCode.InternalServerError),
            ("phone_conversation_messages_empty", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
        ]);
        var logger = new CapturingLogger<CallLogCopier>();

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot, logger).CopyAsync(CallId, Cancel));

        Assert.Equal(3, chatwoot.Requests.Count(r => r.Url == $"{Account}/conversations/31/toggle_status"));
        Assert.Equal($"POST {Account}/conversations/31/messages", $"{chatwoot.Requests[^1].Method} {chatwoot.Requests[^1].Url}");

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal($"Call log could not resolve conversation 31 for call {CallId}; its note is posted anyway.", warning.Message);
        Assert.IsType<HttpRequestException>(warning.Exception);
    }

    [Fact]
    public async Task AResolveThatFailsTwiceWaitsTheConfiguredTimesOnTheInjectedClock()
    {
        var chatwoot = new ReplayingHandler(
        [
            ("message_search_none", HttpStatusCode.OK),
            ("contacts_none", HttpStatusCode.OK),
            ("phone_contact_created", HttpStatusCode.OK),
            ("phone_contact_conversations_none", HttpStatusCode.OK),
            ("phone_conversation_created", HttpStatusCode.OK),
            (null, HttpStatusCode.InternalServerError),
            (null, HttpStatusCode.InternalServerError),
            ("phone_conversation_resolved", HttpStatusCode.OK),
            ("phone_conversation_messages_empty", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
        ]);
        var clock = new NoWaitClock();

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot, new CallLogOptions(), clock).CopyAsync(CallId, Cancel));

        Assert.Equal(3, chatwoot.Requests.Count(r => r.Url == $"{Account}/conversations/31/toggle_status"));
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)], clock.Delays);
    }

    [Fact]
    public async Task AnExistingConversationChatwootHandsBackFromTheCreateIsNotResolvedButGetsTheNote()
    {
        var chatwoot = new AnsweringHandler(
            ChatwootPayloads.Read("message_search_none"),
            ChatwootPayloads.Read("contacts_none"),
            ChatwootPayloads.Read("phone_contact_created"),
            ChatwootPayloads.Read("phone_contact_conversations_none"),
            ChatwootPayloads.ConversationCreatedWithAMessage(),
            ChatwootPayloads.Read("phone_conversation_messages_empty"),
            "{}");
        var logger = new CapturingLogger<CallLogCopier>();

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot, logger).CopyAsync(CallId, Cancel));

        Assert.DoesNotContain(chatwoot.Requests, r => r.Url.EndsWith("/toggle_status", StringComparison.Ordinal));
        Assert.Equal(("POST", $"{Account}/conversations/31/messages"), chatwoot.Requests[^1]);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal(
            $"Chatwoot answered the create with an existing conversation; not resolving it. Conversation 31, call {CallId}.",
            warning.Message);
    }

    [Fact]
    public async Task AConversationItFoundIsNeverResolved()
    {
        var chatwoot = new ReplayingHandler(["message_search_none", "phone_contacts_found", "phone_contact_conversations", "phone_conversation_messages_empty", null]);

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        AssertGoToWasRead();

        Assert.DoesNotContain(chatwoot.Requests, r => r.Url.EndsWith("/toggle_status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACallAlreadyInTheConversationIsNotCopiedAgain()
    {
        var chatwoot = new ReplayingHandler(["message_search_none", "phone_contacts_found", "phone_contact_conversations", "phone_conversation_messages"]);

        Assert.Equal(CallLogResult.AlreadyCopied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        Assert.Equal([ReportUrl], _goto.Requests.Select(r => r.Url));

        Assert.DoesNotContain(chatwoot.Requests, r => r.Method == "POST");
    }

    [Fact]
    public async Task ACallWhoseNoteSearchFindsItIsNotReadFromGoTo()
    {
        var chatwoot = new ReplayingHandler("message_search_found");

        Assert.Equal(CallLogResult.AlreadyCopied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        Assert.Empty(_goto.Requests);
        Assert.Single(chatwoot.Requests);
    }

    [Fact]
    public async Task ASearchThatFailsNeverStopsTheCopyAndWarnsWithTheCallIdOnly()
    {
        var chatwoot = new ReplayingHandler(
        [
            (null, HttpStatusCode.InternalServerError),
            ("phone_contacts_found", HttpStatusCode.OK),
            ("phone_contact_conversations", HttpStatusCode.OK),
            ("phone_conversation_messages_empty", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
        ]);
        var logger = new CapturingLogger<CallLogCopier>();

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot, logger).CopyAsync(CallId, Cancel));

        Assert.Equal($"POST {Account}/conversations/31/messages", $"{chatwoot.Requests[^1].Method} {chatwoot.Requests[^1].Url}");

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Equal($"Call log could not search for call {CallId}'s note; the conversation is checked instead.", warning.Message);
        Assert.IsType<HttpRequestException>(warning.Exception);
    }

    [Fact]
    public async Task ASearchCancelledWithTheCopyIsNotSwallowedAndNothingElseIsAsked()
    {
        using var cancelled = new CancellationTokenSource();
        var chatwoot = new CancellingHandler(cancelled);
        var logger = new CapturingLogger<CallLogCopier>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Copier(chatwoot, logger).CopyAsync(CallId, cancelled.Token));

        Assert.Equal(1, chatwoot.Requests);
        Assert.Empty(_goto.Requests);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AKnownContactWithNoPlaceInTheInboxIsAddedBeforeItsConversation()
    {
        var chatwoot = new ReplayingHandler(
            ["message_search_none", "phone_contacts_found_no_inbox", "phone_contact_conversations_none", "phone_contact_inbox_created", "phone_conversation_created", "phone_conversation_resolved", "phone_conversation_messages_empty", null]);

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        AssertGoToWasRead();

        Assert.Equal(
            ["GET", "GET", "GET", "POST", "POST", "POST", "GET", "POST"],
            chatwoot.Requests.Select(r => r.Method));
        Assert.Equal($"{Account}/contacts/7/contact_inboxes", chatwoot.Requests[3].Url);
    }

    [Fact]
    public async Task ANumberTakenAMomentAgoCopiesIntoTheContactThatHasIt()
    {
        var chatwoot = new ReplayingHandler(
        [
            ("message_search_none", HttpStatusCode.OK),
            ("contacts_none", HttpStatusCode.OK),
            ("contact_phone_taken", HttpStatusCode.UnprocessableEntity),
            ("phone_contacts_found", HttpStatusCode.OK),
            ("phone_contact_conversations", HttpStatusCode.OK),
            ("phone_conversation_messages_empty", HttpStatusCode.OK),
            (null, HttpStatusCode.OK),
        ]);

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot).CopyAsync(CallId, Cancel));

        AssertGoToWasRead();

        Assert.Equal(
            [
                $"GET {Account}/search/messages?q=GoTo call {CallId}",
                $"GET {Account}/contacts/search?q=%2B12015550100",
                $"POST {Account}/contacts",
                $"GET {Account}/contacts/search?q=%2B12015550100",
                $"GET {Account}/contacts/7/conversations",
                $"GET {Account}/conversations/31/messages",
                $"POST {Account}/conversations/31/messages",
            ],
            chatwoot.Requests.Select(r => $"{r.Method} {r.Url}"));
    }

    [Fact]
    public async Task AMenuHangUpOnlyAsksChatwootWhetherItIsCopied()
    {
        var goTo = new ReplayingHandler("report_menu_hangup") { Folder = "GoTo" };
        await using var services = GoToTestServices.Build(goTo);
        var chatwoot = new ReplayingHandler("message_search_none");

        Assert.Equal(CallLogResult.Skipped, await Copier(chatwoot, services).CopyAsync("229bdd49-32b1-3180-8e2e-6ccf051f388d", Cancel));

        Assert.Equal(
            [$"GET {Account}/search/messages?q=GoTo call 229bdd49-32b1-3180-8e2e-6ccf051f388d"],
            chatwoot.Requests.Select(r => $"{r.Method} {r.Url}"));
    }

    [Fact]
    public async Task AReportThatNeverArrivesGivesUpAfterEveryWait()
    {
        var goTo = new ReplayingHandler(payload: null, HttpStatusCode.NotFound) { Folder = "GoTo" };
        await using var services = GoToTestServices.Build(goTo);
        var chatwoot = new ReplayingHandler("message_search_none");

        Assert.Equal(CallLogResult.NotReady, await Copier(chatwoot, services, logger: null, TimeSpan.Zero, TimeSpan.Zero).CopyAsync("not-ended-yet", Cancel));

        Assert.Equal(3, goTo.Requests.Count);
        Assert.Single(chatwoot.Requests);
    }

    [Fact]
    public async Task ANoteIsStillPostedWithoutTheBrandWhenGoToCannotListTheLines()
    {
        var goTo = new ReplayingHandler([("report_answered", HttpStatusCode.OK), (null, HttpStatusCode.InternalServerError)]) { Folder = "GoTo" };
        await using var services = GoToTestServices.Build(goTo, new NoWaitClock());
        var chatwoot = new ReplayingHandler(["message_search_none", "phone_contacts_found", "phone_contact_conversations", "phone_conversation_messages_empty", null]);

        Assert.Equal(CallLogResult.Copied, await Copier(chatwoot, services).CopyAsync(CallId, Cancel));

        var note = JsonNode.Parse(chatwoot.Requests[^1].Body)!["content"]!.GetValue<string>();
        Assert.Contains("\nLine: +1 800-258-8511 → Service - Premium\n", note);
    }

    private void AssertGoToWasRead()
        => Assert.Equal(
            [
                ReportUrl,
                "https://api.goto.com/voice-admin/v1/extensions?accountKey=1234567890123456789&pageSize=100",
                "https://api.goto.com/voice-admin/v1/phone-numbers?accountKey=1234567890123456789&pageSize=100",
            ],
            _goto.Requests.Select(r => r.Url));

    public ValueTask DisposeAsync() => _gotoServices.DisposeAsync();

    private CallLogCopier Copier(HttpMessageHandler chatwoot) => Copier(chatwoot, _gotoServices);

    private CallLogCopier Copier(HttpMessageHandler chatwoot, ILogger<CallLogCopier> logger) => Copier(chatwoot, _gotoServices, logger: logger);

    private CallLogCopier Copier(HttpMessageHandler chatwoot, CallLogOptions options, TimeProvider clock)
        => Copier(chatwoot, _gotoServices, options, clock, NullLogger<CallLogCopier>.Instance);

    private static CallLogCopier Copier(
        HttpMessageHandler chatwoot, ServiceProvider gotoServices, ILogger<CallLogCopier>? logger = null, params TimeSpan[] waits)
        => Copier(
            chatwoot,
            gotoServices,
            new CallLogOptions { ReportWaits = waits.Length > 0 ? waits : [TimeSpan.Zero], ResolveWaits = [TimeSpan.Zero, TimeSpan.Zero] },
            TimeProvider.System,
            logger ?? NullLogger<CallLogCopier>.Instance);

    private static CallLogCopier Copier(
        HttpMessageHandler chatwoot, ServiceProvider gotoServices, CallLogOptions options, TimeProvider clock, ILogger<CallLogCopier> logger)
        => new(
            gotoServices.GetRequiredService<GoToClient>(),
            gotoServices.GetRequiredService<GoToCompanyLines>(),
            new ChatwootClient(new HttpClient(chatwoot), Options.Create(new ChatwootOptions
            {
                BaseUrl = "http://chatwoot.test/",
                AccountId = 2,
                InboxId = 1,
                BotToken = "bot-token",
                ServiceToken = "service-token",
            })),
            Options.Create(options),
            clock,
            logger);

    /// <summary>Takes the copy's token as soon as a request arrives, so that request is the one cancelled.</summary>
    private sealed class CancellingHandler(CancellationTokenSource copy) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            copy.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
