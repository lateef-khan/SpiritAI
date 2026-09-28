using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.GoTo;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Handoffs.Callback;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Callback;

/// <summary>
/// A ring on line 8625 from +1 201-555-0123 (<c>call_ringing</c>). In GoTo the line is Dana's; in
/// a live Chatwoot the number is contact 1's, whose open conversation 1 is given to team
/// <c>service</c>, and Dana is agent 3.
/// </summary>
public sealed class CallRingAlertTests : IAsyncDisposable
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly ReplayingHandler _goto = new(["users", "admin_users"]) { Folder = "GoTo" };
    private readonly ServiceProvider _gotoServices;

    public CallRingAlertTests() => _gotoServices = GoToTestServices.Build(_goto);

    [Fact]
    public async Task ARingPostsOneNoteThatMentionsWhoseLineItIs()
    {
        var chatwoot = new ReplayingHandler(["contacts_found", "contact_conversations", "agents", null]);
        var alert = Alert(chatwoot);
        var ring = GoToCallTests.Read("call_ringing");

        await alert.HandleAsync(ring, Cancel);
        await alert.HandleAsync(ring, Cancel);

        Assert.Equal(
            [
                "GET http://chatwoot.test/api/v1/accounts/2/contacts/search?q=%2B12015550123",
                "GET http://chatwoot.test/api/v1/accounts/2/contacts/1/conversations",
                "GET http://chatwoot.test/api/v1/accounts/2/agents",
                "POST http://chatwoot.test/api/v1/accounts/2/conversations/1/messages",
            ],
            chatwoot.Requests.Select(r => $"{r.Method} {r.Url}"));

        var note = JsonDocument.Parse(chatwoot.Requests[^1].Body).RootElement;
        Assert.Equal(
            "📞 +1 201-555-0123 is calling now — code 1. [@Dana Test](mention://user/3/Dana%20Test)",
            note.GetProperty("content").GetString());
        Assert.True(note.GetProperty("private").GetBoolean());
        Assert.Equal("bot-token", chatwoot.Requests[^1].Token);
    }

    [Fact]
    public async Task ALineNoMemberOfStaffOwnsMentionsTheConversationsTeam()
    {
        var chatwoot = new ReplayingHandler(["contacts_found", "contact_conversations", "agents_no_match", null]);

        await Alert(chatwoot).HandleAsync(GoToCallTests.Read("call_ringing"), Cancel);

        Assert.Equal(
            "📞 +1 201-555-0123 is calling now — code 1. [@service](mention://team/1/service)",
            JsonDocument.Parse(chatwoot.Requests[^1].Body).RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ACallerWhoNeverAskedForAPersonGetsNoNote()
    {
        var chatwoot = new ReplayingHandler("contacts_none");
        var alert = Alert(chatwoot);
        var ring = GoToCallTests.Read("call_ringing");

        await alert.HandleAsync(ring, Cancel);
        await alert.HandleAsync(ring, Cancel);

        var request = Assert.Single(chatwoot.Requests);
        Assert.Equal("GET", request.Method);
    }

    public ValueTask DisposeAsync() => _gotoServices.DisposeAsync();

    private CallRingAlert Alert(ReplayingHandler chatwoot)
    {
        var client = new ChatwootClient(new HttpClient(chatwoot), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            InboxIdentifier = "inbox-key",
            BotToken = "bot-token",
            ServiceToken = "service-token",
        }));

        return new(
            client,
            new CallStaff(client, _gotoServices.GetRequiredService<GoToStaffDirectory>(), TestHybridCache.Create()),
            TestHybridCache.Create(),
            Options.Create(new CallbackOptions()),
            NullLogger<CallRingAlert>.Instance);
    }
}
