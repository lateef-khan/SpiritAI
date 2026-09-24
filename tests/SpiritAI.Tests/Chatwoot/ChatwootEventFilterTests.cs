using System.Text.Json;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// What Spirit does about each webhook of one real conversation. The payloads were recorded from
/// Chatwoot v4.18.0-ce on 2026-09-23, with names and emails replaced: the visitor writes, the bot
/// answers, the bot hands off, staff take it, type, reply, leave a note, and resolve.
/// </summary>
public sealed class ChatwootEventFilterTests
{
    [Theory]
    [InlineData("conversation_created", ChatwootAction.Ignore)]
    [InlineData("visitor_message", ChatwootAction.Ignore)]
    [InlineData("bot_message", ChatwootAction.Ignore)]
    [InlineData("status_open", ChatwootAction.Ignore)]
    [InlineData("assigned", ChatwootAction.Assigned)]
    [InlineData("typing_on", ChatwootAction.TypingOn)]
    [InlineData("typing_off", ChatwootAction.TypingOff)]
    [InlineData("staff_message", ChatwootAction.StaffMessage)]
    [InlineData("private_note", ChatwootAction.Ignore)]
    [InlineData("resolved", ChatwootAction.Resolved)]
    public void EachRecordedWebhookAsksForItsAction(string payload, ChatwootAction expected)
        => Assert.Equal(expected, ChatwootEventFilter.ActionOf(Read(payload)));

    [Fact]
    public void AStaffMessageCarriesTheWordsTheSpiritChatAndWhoWroteThem()
    {
        var e = Read("staff_message");

        Assert.Equal(("conv_1", "Hi, this is staff", "Dana Staff"), (e.SpiritConversationId, e.Content, e.ActorName));
    }

    [Fact]
    public void AnAssignmentCarriesTheAssigneesName()
        => Assert.Equal("Dana Staff", Read("assigned").AssigneeName);

    [Fact]
    public void AConversationThatNamesNoSpiritChatIsIgnored()
    {
        var body = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(PathOf("staff_message")).Replace("spirit_conversation_id", "other", StringComparison.Ordinal));

        Assert.Equal(ChatwootAction.Ignore, ChatwootEventFilter.ActionOf(ChatwootEvent.Parse(body)!));
    }

    private static ChatwootEvent Read(string payload)
        => ChatwootEvent.Parse(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(PathOf(payload))))!;

    private static string PathOf(string payload) => Path.Combine(AppContext.BaseDirectory, "Chatwoot", "Payloads", payload + ".json");
}
