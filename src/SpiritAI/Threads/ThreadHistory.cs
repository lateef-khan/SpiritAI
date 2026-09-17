using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AgentCore.Application.Calls;
using AgentCore.Application.Transcript;
using AgentCore.Domain.Sources;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Threads;

/// <summary>One thing a message is made of, as assistant-ui switches on it.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ThreadTextPart), "text")]
[JsonDerivedType(typeof(ThreadToolCallPart), "tool-call")]
[JsonDerivedType(typeof(ThreadSourcePart), "source")]
[JsonDerivedType(typeof(ThreadFilePart), "file")]
public abstract record ThreadPart;

/// <summary>Words.</summary>
public sealed record ThreadTextPart(string Text) : ThreadPart;

/// <summary>
/// A file the reply produced, as the stream's <c>agentcore_file</c> frame spells it. Not an
/// assistant-ui part: the browser decides whether to draw it as a picture or a download, so the
/// same rule serves a live turn and a restored one.
/// </summary>
/// <param name="Url">Where the browser fetches it from. Signed per read, and short-lived.</param>
public sealed record ThreadFilePart(string Name, string MediaType, long Length, string Url) : ThreadPart;

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

/// <summary>Whether a reply is still arriving.</summary>
public sealed record ThreadMessageStatus(string Type);

/// <summary>
/// The slot on a message that belongs to the application.
/// </summary>
public sealed record ThreadMessageMetadata
{
    /// <summary>
    /// Gets the application's own fields. One is written: <c>speaker</c>, who wrote a message of
    /// the human phase, in the shape <see cref="SpeakerProperty"/> stores. The browser reads it from
    /// <c>metadata.custom.speaker</c> and draws the name above the message.
    /// </summary>
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
        => Of(call, rows, new Dictionary<string, ThreadPart>(StringComparer.Ordinal));

    /// <summary>Reads one whole call, linking every file the store still holds.</summary>
    /// <param name="call">The call's row.</param>
    /// <param name="calls">The door to the stored call. It reads the rows and links the files.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The conversation, oldest message first, chained by parent.</returns>
    public static async Task<ThreadHistory> ReadAsync(
        CallRecord call,
        CallRepository calls,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(calls);

        var rows = await calls.ReadAsync(call.CallId, cancellationToken).ConfigureAwait(false);

        var links = await calls
            .LinkFilesAsync(call.CallId, rows.Select(row => row.Content), cancellationToken)
            .ConfigureAwait(false);

        return Of(call, rows, ThreadFiles.PartsOf(links));
    }

    /// <summary>Reads one whole call into the shape the browser restores a thread from, with its files linked.</summary>
    /// <param name="call">The call's row. It supplies the clock store 1 does not keep.</param>
    /// <param name="rows">Every stored message of the call. Order does not matter.</param>
    /// <param name="files">The part for each file the store still holds, from <see cref="ThreadFiles.PartsOf"/>.</param>
    /// <returns>The conversation, oldest message first, chained by parent.</returns>
    public static ThreadHistory Of(CallRecord call, IReadOnlyList<CallMessage> rows, IReadOnlyDictionary<string, ThreadPart> files)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(files);

        List<ThreadHistoryItem> messages = [];
        string? parentId = null;

        foreach (var turn in Group(rows))
        {
            var message = Build(call, turn, files);
            messages.Add(new ThreadHistoryItem(parentId, message));

            // The head is the last message, and after the loop this is it. A separate pass to find
            // it could disagree with the chain; this cannot.
            parentId = message.Id;
        }

        return new ThreadHistory(parentId, messages);
    }

    /// <summary>Gathers the rows that become one restored message.</summary>
    private static IEnumerable<List<CallMessage>> Group(IReadOnlyList<CallMessage> rows)
    {
        List<CallMessage>? open = null;

        var openTurn = -1;

        string? openSpeaker = null;

        var openHuman = false;
        
        foreach (var row in rows.OrderBy(row => row.Ordinal))
        {
            var agentSide = row.Content.Role == ChatRole.Assistant || row.Content.Role == ChatRole.Tool;
            
            var human = SpeakerKey(row) is { } key
                && JsonDocument.Parse(key).RootElement.TryGetProperty("kind", out var kind)
                && kind.GetString() == "human";

            if (agentSide && open is not null && !human && !openHuman && openTurn == row.TurnIndex && openSpeaker == SpeakerKey(row))
            {
                open.Add(row);
                continue;
            }

            if (open is not null)
            {
                yield return open;
                open = null;
                openHuman = false;
            }

            if (agentSide)
            {
                open = [row];
                openTurn = row.TurnIndex;
                openSpeaker = SpeakerKey(row);
                openHuman = human;
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

    /// <summary>Who a row speaks as, as the stored JSON spells it. No entry means the agent.</summary>
    private static string? SpeakerKey(CallMessage row)
        => SpeakerProperty.Read(row.Content)?.GetRawText();

    private static ThreadHistoryMessage Build(CallRecord call, List<CallMessage> turn, IReadOnlyDictionary<string, ThreadPart> files)
    {
        var first = turn[0];
        var role = RoleOf(first.Content.Role);

        List<ThreadToolCallPart> tools = [];
        Dictionary<string, int> toolAt = new(StringComparer.Ordinal);
        List<ThreadSourcePart> sources = [];
        List<ThreadPart> attached = [];
        List<string> utterances = [];

        foreach (var row in turn)
        {
            StringBuilder words = new();

            foreach (var content in row.Content.Contents)
            {
                switch (content)
                {
                    case TextContent text:
                        words.Append(text.Text);
                        break;

                    case FileContent file when files.TryGetValue(file.Name, out var part) && !attached.Contains(part):
                        attached.Add(part);
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

                    default:
                        break;
                }
            }

            if (words.Length > 0)
            {
                utterances.Add(words.ToString());
            }
        }

        List<ThreadPart> parts = [.. tools, .. sources];

        if (utterances.Count > 0)
        {
            parts.Add(new ThreadTextPart(string.Join("\n\n", utterances)));
        }

        parts.AddRange(attached);

        return new ThreadHistoryMessage(
            // Positional, because store 1 keeps no message id of its own. It is stable only for as
            // long as a message keeps the ordinal it was written under.
            $"{first.CallId}:{first.Ordinal}",
            role,
            parts,
            first.Content.CreatedAt ?? call.CreatedAt,
            MetadataOf(first.Content))
        {
            Status = role == "assistant" ? new ThreadMessageStatus("complete") : null,
            Attachments = role == "user" ? [] : null,
        };
    }

    private static ThreadMessageMetadata MetadataOf(ChatMessage content)
        => SpeakerProperty.Read(content) is { } speaker
            ? new ThreadMessageMetadata
            {
                Custom = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [SpeakerProperty.Name] = speaker },
            }
            : new ThreadMessageMetadata();

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
