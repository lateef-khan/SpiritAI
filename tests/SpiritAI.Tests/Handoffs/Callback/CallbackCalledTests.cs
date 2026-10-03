using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.GoTo;
using SpiritAI.Handoffs.Callback;
using SpiritAI.Tests.Database;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Callback;

/// <summary>
/// Staff dial a number a visitor left. In GoTo, line 8625 is Dana's and line 8654
/// (<c>call_outbound</c>) has no owner; in a live Chatwoot, conversation 1 waits for a call back
/// with labels <c>sales</c> and <c>callback</c>, and Dana is agent 3.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CallbackCalledTests : IAsyncDisposable
{
    private const string Account = "http://chatwoot.test/api/v1/accounts/2";

    private static readonly GoToCall FromDana = new(
        "call-1", "ACTIVE", Outbound: true, "+12015550123", [new GoToCallLine("d3d10a08-2269-4872-a904-261c68494270", "8625", "CONNECTED")]);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly ReplayingHandler _goto = new(["users", "admin_users"]) { Folder = "GoTo" };
    private readonly ServiceProvider _gotoServices;
    private readonly PostgresFixture _fixture;

    public CallbackCalledTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _gotoServices = GoToTestServices.Build(_goto);
    }

    [Fact]
    public async Task DialingAWaitingNumberGivesTheChatToWhoeverDialedAndKeepsItInTheQueue()
    {
        await DeskStaffSeed.DanaAsync(_fixture);
        var chatwoot = new ReplayingHandler(["conversations_waiting", null]);

        await Handler(chatwoot).HandleAsync(FromDana, Cancel);

        Assert.Equal(
            [
                $"POST {Account}/conversations/filter?page=1 service-token",
                $"POST {Account}/conversations/1/assignments bot-token",
            ],
            chatwoot.Requests.Select(r => $"{r.Method} {r.Url} {r.Token}"));

        Assert.Equal(
            """{"payload":[{"attribute_key":"callback_phone","filter_operator":"equal_to","values":["+12015550123"],"query_operator":"and"},{"attribute_key":"labels","filter_operator":"equal_to","values":["callback"],"query_operator":null}]}""",
            Plain(chatwoot.Requests[0].Body));
        Assert.Equal("""{"assignee_id":3}""", chatwoot.Requests[1].Body);
    }

    [Fact]
    public async Task TheNextEventOfTheSameCallChangesNothing()
    {
        await DeskStaffSeed.DanaAsync(_fixture);
        var chatwoot = new ReplayingHandler(["conversations_waiting", null]);
        var handler = Handler(chatwoot);

        await handler.HandleAsync(FromDana, Cancel);
        await handler.HandleAsync(FromDana with { State = "ENDING" }, Cancel);

        Assert.Equal(2, chatwoot.Requests.Count);
    }

    [Fact]
    public async Task ALineWhoseOwnerIsNoSpiritPerson_ChangesNothing()
    {
        await DeskStaffSeed.NoDanaAsync(_fixture);
        var chatwoot = new ReplayingHandler("conversations_waiting");

        await Handler(chatwoot).HandleAsync(GoToCallTests.Read("call_outbound"), Cancel);

        Assert.Empty(chatwoot.Requests);
    }

    [Fact]
    public async Task DialingANumberNobodyLeftChangesNothing()
    {
        await DeskStaffSeed.DanaAsync(_fixture);
        var chatwoot = new ReplayingHandler(["conversations_waiting_none"]);

        await Handler(chatwoot).HandleAsync(FromDana, Cancel);

        Assert.DoesNotContain(chatwoot.Requests, r => r.Url.EndsWith("/assignments", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnInboundCallIsNotACallBack()
    {
        var chatwoot = new ReplayingHandler("conversations_waiting");

        await Handler(chatwoot).HandleAsync(GoToCallTests.Read("call_ringing"), Cancel);

        Assert.Empty(chatwoot.Requests);
    }

    public ValueTask DisposeAsync() => _gotoServices.DisposeAsync();

    /// <summary>The JSON without escapes, such as <c>+</c> for <c>+</c>, so it reads as written.</summary>
    private static string Plain(string json)
        => JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private CallbackCalled Handler(ReplayingHandler chatwoot)
    {
        var options = Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            BotToken = "bot-token",
            ServiceToken = "service-token",
        });
        var client = new ChatwootClient(new HttpClient(chatwoot), options);

        return new CallbackCalled(
            client,
            new ChatwootConversationTags(new HttpClient(chatwoot), options),
            new CallStaff(_fixture.Open(), _gotoServices.GetRequiredService<GoToStaffDirectory>()),
            TestHybridCache.Create(),
            NullLogger<CallbackCalled>.Instance);
    }
}
