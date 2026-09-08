using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AgentCore.Application.Calls;
using AgentCore.Application.Transcript;
using AgentCore.Domain.Sources;

using Microsoft.Extensions.AI;

namespace SpiritAI.Threads;

/// <summary>One thing a message is made of, as assistant-ui switches on it.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ThreadTextPart), "text")]
[JsonDerivedType(typeof(ThreadToolCallPart), "tool-call")]
[JsonDerivedType(typeof(ThreadSourcePart), "source")]
[JsonDerivedType(typeof(ThreadDataPart), "data")]
public abstract record ThreadPart;

/// <summary>Words.</summary>
public sealed record ThreadTextPart(string Text) : ThreadPart;

/// <summary>A tool the host ran, with its answer folded back in.</summary>
public sealed record ThreadToolCallPart(
    string ToolCallId,
    string ToolName,
    JsonElement Args,
    string ArgsText) : ThreadPart
{
    /// <summary>What the tool answered. Absent means it never did.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }

    /// <summary>Whether the answer was a failure.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }
}

/// <summary>Where an answer came from.</summary>
/// <param name="ParentId">The tool call that cited it, which is how assistant-ui ties the two.</param>
public sealed record ThreadSourcePart(
    string Id,
    string SourceType,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url,
    string MediaType,
    string ParentId,
    ThreadSourceMetadata ProviderMetadata) : ThreadPart;

/// <summary>What is ours rather than assistant-ui's, under the name it looks for.</summary>
public sealed record ThreadSourceMetadata(ThreadSourceOrigin Agentcore);

/// <summary>Which producer cited a source, and where inside it the citation sits.</summary>
public sealed record ThreadSourceOrigin(string Origin, string Locator);

/// <summary>Something the host asked the browser to draw.</summary>
public sealed record ThreadDataPart(string Name, JsonElement Data) : ThreadPart;

/// <summary>Whether a reply is still arriving.</summary>
public sealed record ThreadMessageStatus(string Type);

/// <summary>
/// The slot on a message that belongs to the application.
/// </summary>
public sealed record ThreadMessageMetadata
{
    /// <summary>Gets the application's own fields. Empty today.</summary>
    public IReadOnlyDictionary<string, JsonElement> Custom { get; init; }
        = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}

/// <summary>One message of a thread, in assistant-ui's shape.</summary>
public sealed record ThreadHistoryMessage(
    string Id,
    string Role,
    IReadOnlyList<ThreadPart> Content,
    DateTimeOffset CreatedAt,
    ThreadMessageMetadata Metadata)
{
    /// <summary>Whether the reply finished. Written on an assistant message and nowhere else.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ThreadMessageStatus? Status { get; init; }

    /// <summary>What the caller attached. Written on a user message; nothing produces one yet.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object>? Attachments { get; init; }
}

/// <summary>One message and the message it answers.</summary>
public sealed record ThreadHistoryItem(string? ParentId, ThreadHistoryMessage Message);

/// <summary>
/// A stored call, as the conversation assistant-ui draws.
/// </summary>
public sealed record ThreadHistory(string? HeadId, IReadOnlyList<ThreadHistoryItem> Messages)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions Readable =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Reads one whole call into the shape the browser restores a thread from.</summary>
    /// <param name="call">The call's row. It supplies the clock store 1 does not keep.</param>
    /// <param name="rows">Every stored message of the call. Order does not matter.</param>
    /// <returns>The conversation, oldest message first, chained by parent.</returns>
    public static ThreadHistory Of(CallRecord call, IReadOnlyList<CallMessage> rows)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(rows);

        List<ThreadHistoryItem> messages = [];
        string? parentId = null;

        foreach (var turn in Group(rows))
        {
            var message = Build(call, turn);
            messages.Add(new ThreadHistoryItem(parentId, message));

            // The head is the last message, and after the loop this is it. A separate pass to find
            // it could disagree with the chain; this cannot.
            parentId = message.Id;
        }

        return new ThreadHistory(parentId, messages);
    }

    /// <summary>Gathers the rows that become one drawn message.</summary>
    private static IEnumerable<List<CallMessage>> Group(IReadOnlyList<CallMessage> rows)
    {
        List<CallMessage>? open = null;
        var openTurn = -1;

        foreach (var row in rows.OrderBy(row => row.Ordinal))
        {
            var agentSide = row.Content.Role == ChatRole.Assistant || row.Content.Role == ChatRole.Tool;

            if (agentSide && open is not null && openTurn == row.TurnIndex)
            {
                open.Add(row);
                continue;
            }

            if (open is not null)
            {
                yield return open;
                open = null;
            }

            if (agentSide)
            {
                open = [row];
                openTurn = row.TurnIndex;
            }
            else
            {
                yield return [row];
            }
        }

        if (open is not null)
        {
            yield return open;
        }
    }

    private static ThreadHistoryMessage Build(CallRecord call, List<CallMessage> turn)
    {
        var first = turn[0];
        var role = RoleOf(first.Content.Role);

        List<ThreadToolCallPart> tools = [];
        Dictionary<string, int> toolAt = new(StringComparer.Ordinal);
        List<ThreadSourcePart> sources = [];
        List<ThreadDataPart> drawn = [];
        StringBuilder words = new();

        foreach (var content in turn.SelectMany(row => row.Content.Contents))
        {
            switch (content)
            {
                case TextContent text:
                    words.Append(text.Text);
                    break;

                case FunctionCallContent called:
                    toolAt[called.CallId] = tools.Count;
                    tools.Add(ToolOf(called));
                    break;

                case FunctionResultContent answered when toolAt.TryGetValue(answered.CallId, out var at):
                    tools[at] = tools[at] with
                    {
                        Result = JsonSerializer.SerializeToElement(answered.Result, Json),
                        IsError = answered.Exception is not null,
                    };
                    break;

                case SourceContent cited:
                    sources.Add(SourceOf(cited));
                    break;

                case RenderContent render:
                    drawn.Add(new ThreadDataPart(render.Name, render.Data));
                    break;

                default:
                    break;
            }
        }

        List<ThreadPart> parts = [.. tools, .. sources];

        if (words.Length > 0)
        {
            parts.Add(new ThreadTextPart(words.ToString()));
        }

        parts.AddRange(drawn);

        return new ThreadHistoryMessage(
            // Positional, because store 1 keeps no message id of its own. It is stable only for as
            // long as a message keeps the ordinal it was written under.
            $"{first.CallId}:{first.Ordinal}",
            role,
            parts,
            first.Content.CreatedAt ?? call.CreatedAt,
            new ThreadMessageMetadata())
        {
            Status = role == "assistant" ? new ThreadMessageStatus("complete") : null,
            Attachments = role == "user" ? [] : null,
        };
    }

    private static ThreadToolCallPart ToolOf(FunctionCallContent called)
    {
        var args = JsonSerializer.SerializeToElement(
            called.Arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal), Json);

        return new ThreadToolCallPart(
            called.CallId,
            called.Name,
            args,
            JsonSerializer.Serialize(args, Readable));
    }

    private static ThreadSourcePart SourceOf(SourceContent cited)
        => new(
            cited.Source.SourceId,
            cited.Source.Kind == SourceKind.Url ? "url" : "document",
            cited.Source.Title,
            cited.Source.Url,
            cited.Source.MediaType,
            cited.CallId,
            new ThreadSourceMetadata(new ThreadSourceOrigin(cited.Source.Origin, cited.Source.Locator)));

    private static string RoleOf(ChatRole role)
    {
        if (role == ChatRole.User)
        {
            return "user";
        }

        return role == ChatRole.System ? "system" : "assistant";
    }
}
