using AgentCore.Application.Conversation;
using AgentCore.Application.Conversation.Memory;
using AgentCore.Application.Ports;

using Microsoft.Extensions.Options;

using SpiritAI.Handoffs.Bot;
using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;
using SpiritAI.Tests.Auth;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>
/// The bot's way to ask for a person, section 9.4 of the handoff spec, and the call back promise of
/// the phone callback spec, section 3.
/// </summary>
public sealed class RequestHumanToolTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private readonly TestTimeProvider _clock = new(new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeHandoffStore _store;
    private readonly IConversations _conversations;
    private readonly RecordingHandoffNotifier _notifier = new();
    private readonly CallbackOptions _callback = new();
    private readonly RequestHumanTool _tool;

    public RequestHumanToolTests()
    {
        _store = new FakeHandoffStore(_clock);
        _conversations = new Conversations(new InMemoryConversationStore(_clock), blobs: null);
        _tool = new RequestHumanTool(
            new HandoffDesk(
                _store,
                _conversations,
                _notifier,
                new FakeStaffPresence(),
                _clock),
            Options.Create(_callback));
    }

    [Fact]
    public async Task InsideAChatTheToolAsksForAPerson()
    {
        var conversationId = await NewChatAsync();

        var answer = await _tool.AskAsync(conversationId, "they want a real person", phone: null, HandoffSummary.Empty, Cancel);

        Assert.Equal(RequestHumanTool.NoPhoneNote, answer.Note);

        var row = Assert.Single(_store.Rows);
        Assert.Equal(conversationId, row.ConversationId);
        Assert.Equal(HandoffStatus.Waiting, row.Status);
        Assert.Equal(HandoffAskedBy.Bot, row.AskedBy);
        Assert.Equal("they want a real person", row.Reason);
        Assert.Empty(_notifier.Events);
    }

    [Fact]
    public async Task TheSummaryGivenWithTheAskLandsOnTheRow()
    {
        var conversationId = await NewChatAsync();
        var summary = new HandoffSummary("XT485 treadmill", "0045210000001234", "Lubricated the belt; no change", "A technician visit");

        await _tool.AskAsync(conversationId, "Belt slips at speed 6", "(201) 555-0123", summary, Cancel);

        var row = Assert.Single(_store.Rows);
        Assert.Equal("XT485 treadmill", row.Product);
        Assert.Equal("0045210000001234", row.Serial);
        Assert.Equal("Lubricated the belt; no change", row.Tried);
        Assert.Equal("A technician visit", row.Wants);
    }

    [Fact]
    public async Task APhoneGivenWithTheAskLandsOnTheRowAndInTheAnswer()
    {
        var conversationId = await NewChatAsync();

        var answer = await _tool.AskAsync(conversationId, "they want a real person", "(201) 555-0123", HandoffSummary.Empty, Cancel);

        var row = Assert.Single(_store.Rows);
        Assert.Equal("+12015550123", row.Phone);
        Assert.Equal(
            $"We will call you at +1 201-555-0123. Your code is {row.Id}. If you call us first, give that code.",
            answer.Note);
    }

    [Fact]
    public async Task ThePromiseSaysWhenStaffCallBack()
    {
        _callback.Promise = "within 2 hours, Mon-Fri 9-5";
        var conversationId = await NewChatAsync();

        var answer = await _tool.AskAsync(conversationId, "they want a real person", "(201) 555-0123", HandoffSummary.Empty, Cancel);

        Assert.StartsWith("We will call you at +1 201-555-0123 within 2 hours, Mon-Fri 9-5. Your code is ", answer.Note);
    }

    [Fact]
    public async Task ANumberThatCannotBeReadIsLeftOffAndSaidSo()
    {
        var conversationId = await NewChatAsync();

        var answer = await _tool.AskAsync(conversationId, "they want a real person", "12", HandoffSummary.Empty, Cancel);

        Assert.Null(Assert.Single(_store.Rows).Phone);
        Assert.Equal(RequestHumanTool.NoPhoneNote + RequestHumanTool.BadPhoneNote, answer.Note);
    }

    private async Task<string> NewChatAsync()
    {
        var conversationId = Guid.NewGuid().ToString("N");
        await _conversations.CreateAsync(conversationId, Cancel);
        return conversationId;
    }
}
