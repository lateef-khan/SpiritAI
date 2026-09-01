using System.Text.Json;

using AgentCore.Application.Calls;
using AgentCore.Application.Transcript;
using AgentCore.Domain.Sources;

using Microsoft.Extensions.AI;

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
        var source = Assert.IsType<ThreadSourcePart>(parts[0]);

        Assert.Equal("kb-7", source.Id);
        Assert.Equal("document", source.SourceType);
        Assert.Equal("Belt tension", source.Title);
        Assert.Equal("call-a", source.ParentId);
    }

    [Fact]
    public void SourcesComeBeforeTheWordsTheySupport()
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
        var parts = Assert.Single(history.Messages).Message.Content;
        Assert.IsType<ThreadSourcePart>(parts[0]);
        Assert.IsType<ThreadTextPart>(parts[1]);
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

    private static CallMessage Row(int ordinal, int turnIndex, ChatMessage message)
        => new("call-1", ordinal, turnIndex, message, $"m{ordinal}");

    private static ChatMessage Called(string callId, string name, IDictionary<string, object?> arguments)
        => new(ChatRole.Assistant, [new FunctionCallContent(callId, name, arguments)]);

    private static ChatMessage Answered(string callId, object result)
        => new(ChatRole.Tool, [new FunctionResultContent(callId, result)]);
}
