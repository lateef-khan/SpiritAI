using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Transcript;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Transcript;

public sealed class UnreadTests
{
    [Fact]
    public void AVisitorLineNobodyHasOpenedIsUnread()
    {
        Assert.True(Unread.Of([Visitor(0, "hello?")], seenOrdinal: null));
    }

    [Fact]
    public void AVisitorLinePastTheMarkIsUnread()
    {
        Assert.True(Unread.Of([Visitor(0, "hello?"), Person(1, "Hi."), Visitor(2, "still there?")], seenOrdinal: 1));
    }

    [Fact]
    public void AVisitorLineAtOrBeforeTheMarkIsRead()
    {
        Assert.False(Unread.Of([Visitor(0, "hello?"), Person(1, "Hi.")], seenOrdinal: 1));
        Assert.False(Unread.Of([Visitor(0, "hello?")], seenOrdinal: 0));
    }

    [Fact]
    public void OnlyTheVisitorMakesAChatUnread()
    {
        Assert.False(Unread.Of([Visitor(0, "hello?"), Bot(1, "Let me get someone."), Note(2, "Dana joined"), Person(3, "Hi.")], seenOrdinal: 0));
    }

    [Fact]
    public void AChatTheVisitorNeverSpokeInIsRead()
    {
        Assert.False(Unread.Of([], seenOrdinal: null));
        Assert.False(Unread.Of([Bot(0, "Hello!")], seenOrdinal: null));
    }

    private static ConversationMessage Visitor(int ordinal, string text)
        => Row(ordinal, new ChatMessage(ChatRole.User, text));

    private static ConversationMessage Bot(int ordinal, string text)
        => Row(ordinal, new ChatMessage(ChatRole.Assistant, text));

    private static ConversationMessage Person(int ordinal, string text)
    {
        var line = new ChatMessage(ChatRole.Assistant, text);
        SpeakerProperty.Attach(line, HandoffSpeaker.Human("Dana", "Support"));
        return Row(ordinal, line);
    }

    private static ConversationMessage Note(int ordinal, string text)
    {
        var line = new ChatMessage(ChatRole.Assistant, text);
        SpeakerProperty.Attach(line, HandoffSpeaker.System());
        return Row(ordinal, line);
    }

    private static ConversationMessage Row(int ordinal, ChatMessage content)
        => new("c1", ordinal, ordinal, content, $"m{ordinal}");
}
