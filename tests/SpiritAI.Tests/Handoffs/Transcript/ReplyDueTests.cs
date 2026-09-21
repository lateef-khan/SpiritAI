using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Transcript;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Transcript;

public sealed class ReplyDueTests
{
    [Fact]
    public void TheVisitorAloneIsWaiting()
    {
        Assert.True(ReplyDue.Of([Visitor(0, "hello?")]));
    }

    [Fact]
    public void TheBotAnsweringAndTheHostNotingDoNotCountAsAReply()
    {
        Assert.True(ReplyDue.Of([Visitor(0, "hello?"), Bot(1, "Let me get someone."), Note(2, "Dana joined")]));
    }

    [Fact]
    public void APersonReplyingClearsTheWait()
    {
        Assert.False(ReplyDue.Of([Visitor(0, "hello?"), Note(1, "Dana joined"), Person(2, "Hi, Dana here.")]));
    }

    [Fact]
    public void TheVisitorSpeakingAgainRenewsIt()
    {
        Assert.True(ReplyDue.Of([Visitor(0, "hello?"), Person(1, "Hi, Dana here."), Visitor(2, "still there?")]));
    }

    [Fact]
    public void AnEmptyTranscriptOwesNothingYet()
    {
        Assert.False(ReplyDue.Of([]));
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
        => new("c1", ordinal, 0, content, $"c1:{ordinal}");
}
