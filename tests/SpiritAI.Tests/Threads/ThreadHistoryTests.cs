using System.Text.Json;

using AgentCore.Application.Calls;
using AgentCore.Application.Transcript;
using AgentCore.Domain.Sources;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.Threads;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// Turning a stored call back into the conversation assistant-ui draws.
/// </summary>
public sealed class ThreadHistoryTests
{
    private static readonly DateTimeOffset Made = DateTimeOffset.Parse("2026-08-31T09:00:00Z", null);

    private static readonly CallRecord Call =
        new("call-1", null, CallStatus.Regular, null, null, Made, null);

    [Fact]
    public void ACallWithNoWordsHasNoMessages()
    {
        var history = ThreadHistory.Of(Call, []);

        Assert.Empty(history.Messages);
        Assert.Null(history.HeadId);
    }

    [Fact]
    public void WhatTheCallerSaidComesBackAsAUserMessage()
    {
        var history = ThreadHistory.Of(Call, [Row(0, 0, new ChatMessage(ChatRole.User, "hello"))]);

        var message = Assert.Single(history.Messages).Message;

        Assert.Equal("user", message.Role);
        Assert.Equal("hello", Assert.IsType<ThreadTextPart>(Assert.Single(message.Content)).Text);
    }

    [Fact]
    public void MessagesAreChainedByParent()
    {
        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.User, "hello")),
            Row(1, 0, new ChatMessage(ChatRole.Assistant, "hi there")),
        ]);

        // The chain and not the order is what assistant-ui reads. A second message whose parent is
        // null starts a second conversation in the same thread.
        Assert.Null(history.Messages[0].ParentId);
        Assert.Equal(history.Messages[0].Message.Id, history.Messages[1].ParentId);
    }

    [Fact]
    public void TheHeadIsTheLastMessage()
    {
        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.User, "hello")),
            Row(1, 0, new ChatMessage(ChatRole.Assistant, "hi there")),
        ]);

        Assert.Equal(history.Messages[^1].Message.Id, history.HeadId);
    }

    [Fact]
    public void AnAssistantMessageSaysItIsFinished()
    {
        var history = ThreadHistory.Of(Call, [Row(0, 0, new ChatMessage(ChatRole.Assistant, "hi"))]);

        // Without a status assistant-ui reads the message as still streaming, and draws a reply that
        // never stops arriving.
        Assert.Equal("complete", Assert.Single(history.Messages).Message.Status?.Type);
    }

    [Fact]
    public void AToolCallAndItsResultBecomeOnePart()
    {
        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.User, "what does it cost?")),
            Row(1, 0, Called("call-a", "price_lookup", new Dictionary<string, object?> { ["sku"] = "T101" })),
            Row(2, 0, Answered("call-a", new { price = 50 })),
            Row(3, 0, new ChatMessage(ChatRole.Assistant, "It is 50.")),
        ]);

        var reply = history.Messages[^1].Message;
        var tool = Assert.IsType<ThreadToolCallPart>(reply.Content[0]);

        Assert.Equal("price_lookup", tool.ToolName);
        Assert.Equal("T101", tool.Args.GetProperty("sku").GetString());
        Assert.Equal(50, tool.Result?.GetProperty("price").GetInt32());
    }

    [Fact]
    public void EveryRowOfOneTurnBecomesOneMessage()
    {
        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.User, "what does it cost?")),
            Row(1, 0, Called("call-a", "price_lookup", new Dictionary<string, object?> { ["sku"] = "T101" })),
            Row(2, 0, Answered("call-a", new { price = 50 })),
            Row(3, 0, new ChatMessage(ChatRole.Assistant, "It is 50.")),
        ]);

        // A tool-calling turn is two assistant rows and a tool row. Drawn as three bubbles it reads
        // as the agent talking to itself.
        Assert.Equal(2, history.Messages.Count);
        Assert.Equal("It is 50.", Assert.IsType<ThreadTextPart>(history.Messages[^1].Message.Content[^1]).Text);
    }

    [Fact]
    public void ANewSpeakerStartsANewMessage()
    {
        var joined = new ChatMessage(ChatRole.Assistant, "Dana joined");
        SpeakerProperty.Attach(joined, HandoffSpeaker.System());
        var reply = new ChatMessage(ChatRole.Assistant, "Try the tension bolt.");
        SpeakerProperty.Attach(reply, HandoffSpeaker.Human("Dana", "Support"));
        var left = new ChatMessage(ChatRole.Assistant, "Dana left");
        SpeakerProperty.Attach(left, HandoffSpeaker.System());

        // The host's lines and the staff reply are consecutive assistant rows of one turn. Drawn
        // as one message they read as "Dana joinedTry the tension bolt.Dana left" under one name.
        var history = ThreadHistory.Of(Call, [Row(0, 7, joined), Row(1, 7, reply), Row(2, 7, left)]);

        Assert.Equal(3, history.Messages.Count);
        Assert.Equal("Dana joined", TextOf(history.Messages[0]));
        Assert.Equal("Try the tension bolt.", TextOf(history.Messages[1]));
        Assert.Equal("Dana left", TextOf(history.Messages[2]));
        Assert.Equal("system", SpeakerKindOf(history.Messages[0]));
        Assert.Equal("human", SpeakerKindOf(history.Messages[1]));
        Assert.Equal("system", SpeakerKindOf(history.Messages[2]));
    }

    [Fact]
    public void ConsecutiveRowsOfOneSpeakerJoinWithABreak()
    {
        var history = ThreadHistory.Of(Call, [
            Row(0, 7, new ChatMessage(ChatRole.Assistant, "First.")),
            Row(1, 7, new ChatMessage(ChatRole.Assistant, "Second.")),
        ]);

        Assert.Equal("First.\n\nSecond.", TextOf(Assert.Single(history.Messages)));
    }

    [Fact]
    public void TwoStaffRepliesOnOneTurnStayTwoMessages()
    {
        var first = new ChatMessage(ChatRole.Assistant, "hello");
        SpeakerProperty.Attach(first, HandoffSpeaker.Human("Matthew H.", "Support"));
        var second = new ChatMessage(ChatRole.Assistant, "hello");
        SpeakerProperty.Attach(second, HandoffSpeaker.Human("Matthew H.", "Support"));
        var third = new ChatMessage(ChatRole.Assistant, "hello");
        SpeakerProperty.Attach(third, HandoffSpeaker.Human("Matthew H.", "Support"));

        // Each reply-box send is its own message. Same speaker, same turn index on the store rows,
        // but merging them draws one bubble with the words stuck together.
        var history = ThreadHistory.Of(Call, [Row(0, 7, first), Row(1, 7, second), Row(2, 7, third)]);

        Assert.Equal(3, history.Messages.Count);
        Assert.Equal("hello", TextOf(history.Messages[0]));
        Assert.Equal("hello", TextOf(history.Messages[1]));
        Assert.Equal("hello", TextOf(history.Messages[2]));
    }

    [Fact]
    public void ATurnThatFailedItsToolSaysSo()
    {
        var history = ThreadHistory.Of(Call, [
            Row(0, 0, Called("call-a", "price_lookup", new Dictionary<string, object?>())),
            Row(1, 0, new ChatMessage(ChatRole.Tool, [
                new FunctionResultContent("call-a", "no such part")
                {
                    Exception = new InvalidOperationException("the catalogue is down."),
                },
            ])),
        ]);

        var tool = Assert.IsType<ThreadToolCallPart>(Assert.Single(history.Messages).Message.Content[0]);

        Assert.True(tool.IsError);
    }

    [Fact]
    public void ACitedSourceComesBackWithTheTurnThatCitedIt()
    {
        var cited = new SourceContent
        {
            CallId = "call-a",
            Source = new SourceReference
            {
                SourceId = "kb-7",
                Kind = SourceKind.Document,
                Title = "Belt tension",
                Origin = "knowledge",
                Locator = "p.27",
                MediaType = "text/markdown",
            },
        };

        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.Assistant, [cited, new TextContent("Tighten it.")])),
        ]);

        var parts = Assert.Single(history.Messages).Message.Content;
        var source = Assert.IsType<ThreadSourcePart>(parts[1]);

        Assert.Equal("kb-7", source.Id);
        Assert.Equal("document", source.SourceType);
        Assert.Equal("Belt tension", source.Title);
        Assert.Equal("call-a", source.ParentId);
    }

    [Fact]
    public void SourcesComeAfterTheWordsTheySupport()
    {
        var cited = new SourceContent
        {
            CallId = "call-a",
            Source = new SourceReference
            {
                SourceId = "kb-7",
                Kind = SourceKind.Document,
                Title = "Belt tension",
                Origin = "knowledge",
            },
        };

        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.Assistant, [new TextContent("Tighten it."), cited])),
        ]);

        // The same order the live turn draws in, so a reloaded thread does not rearrange itself.
        // A source draws nothing in place, and last it never shifts the words above it.
        var parts = Assert.Single(history.Messages).Message.Content;
        Assert.IsType<ThreadTextPart>(parts[0]);
        Assert.IsType<ThreadSourcePart>(parts[1]);
    }

    [Fact]
    public void WordsAndToolsKeepTheOrderTheyWereSaidIn()
    {
        // A sentence before a tool, the tool, then a program: three parts, so the program starts
        // on a line of its own rather than mid-sentence.
        var history = ThreadHistory.Of(Call, [
            Row(0, 0, new ChatMessage(ChatRole.Assistant, [
                new TextContent("Checking the orders."),
                new FunctionCallContent("call-a", "read_records", new Dictionary<string, object?>()),
            ])),
            Row(1, 0, new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-a", "42 rows")])),
            Row(2, 0, new ChatMessage(ChatRole.Assistant, [new TextContent("root = Card([])")])),
        ]);

        var parts = Assert.Single(history.Messages).Message.Content;
        Assert.Equal("Checking the orders.", Assert.IsType<ThreadTextPart>(parts[0]).Text);
        Assert.Equal("call-a", Assert.IsType<ThreadToolCallPart>(parts[1]).ToolCallId);
        Assert.Equal("root = Card([])", Assert.IsType<ThreadTextPart>(parts[2]).Text);
    }

    [Fact]
    public void EachPartNamesItsKindOnTheWire()
    {
        var history = ThreadHistory.Of(Call, [Row(0, 0, new ChatMessage(ChatRole.User, "hello"))]);

        var json = JsonSerializer.Serialize(history, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // assistant-ui switches on `type`, so the discriminator is the contract and not a detail.
        Assert.Contains("\"type\":\"text\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AMessageWithNoTimeOfItsOwnTakesTheCalls()
    {
        var history = ThreadHistory.Of(Call, [Row(0, 0, new ChatMessage(ChatRole.User, "hello"))]);

        // assistant-ui refuses a message with no createdAt, and store 1 keeps no per-message clock.
        Assert.Equal(Made, Assert.Single(history.Messages).Message.CreatedAt);
    }

    [Fact]
    public void AMessageNamesItsSpeakerWhereTheBrowserLooks()
    {
        var reply = new ChatMessage(ChatRole.Assistant, "Try the tension bolt.");
        SpeakerProperty.Attach(reply, HandoffSpeaker.Human("Dana R.", "Support"));

        var history = ThreadHistory.Of(Call, [Row(0, 0, reply)]);

        // AgentCoreRuntime.ts reads metadata.custom.speaker, in the Speaker shape of transport.ts.
        var speaker = Assert.Single(history.Messages).Message.Metadata.Custom["speaker"];
        Assert.Equal("human", speaker.GetProperty("kind").GetString());
        Assert.Equal("Dana R.", speaker.GetProperty("name").GetString());
        Assert.Equal("Support", speaker.GetProperty("detail").GetString());
    }

    private static string TextOf(ThreadHistoryItem item)
        => Assert.IsType<ThreadTextPart>(item.Message.Content[^1]).Text;

    private static string? SpeakerKindOf(ThreadHistoryItem item)
        => item.Message.Metadata.Custom.TryGetValue("speaker", out var speaker)
            ? speaker.GetProperty("kind").GetString()
            : null;

    private static CallMessage Row(int ordinal, int turnIndex, ChatMessage message)
        => new("call-1", ordinal, turnIndex, message, $"m{ordinal}");

    private static ChatMessage Called(string callId, string name, IDictionary<string, object?> arguments)
        => new(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]);

    private static ChatMessage Answered(string callId, object result)
        => new(ChatRole.Tool, [new FunctionResultContent(callId, result)]);
}
