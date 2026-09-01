using System.Text.Json;
using System.Text.Json.Serialization;

using AgentCore.Application.Calls;

namespace SpiritAI.Threads;

/// <summary>
/// The shapes the browser reads, which are assistant-ui's and not AgentCore's.
/// </summary>
public sealed record ThreadSummary(
    string RemoteId,
    string Status,
    string? ExternalId,
    string? Title,
    DateTimeOffset? LastMessageAt,
    JsonElement? Custom)
{
    /// <summary>The two values <c>status</c> takes, as the wire spells them.</summary>
    public const string Regular = "regular";

    /// <inheritdoc cref="Regular" />
    public const string Archived = "archived";

    /// <summary>Describes one stored call to the browser.</summary>
    /// <param name="record">The row store 0 holds.</param>
    /// <returns>The row, with the host's own bookkeeping taken back out of <c>custom</c>.</returns>
    public static ThreadSummary Of(CallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ThreadSummary(
            record.CallId,
            record.Status == CallStatus.Archived ? Archived : Regular,
            record.ExternalId,
            record.Title,
            record.LastMessageAt,
            ThreadEnvelope.AppOf(record.Custom));
    }

    /// <summary>Reads the wire's spelling of a status.</summary>
    /// <param name="text">What the caller sent.</param>
    /// <param name="status">The status it named.</param>
    /// <returns><see langword="true"/> when the value was one this host knows.</returns>
    public static bool TryReadStatus(string? text, out CallStatus status)
    {
        status = CallStatus.Regular;

        switch (text)
        {
            case Regular:
                return true;
            case Archived:
                status = CallStatus.Archived;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>One page of a caller's threads.</summary>
/// <param name="Threads">The page's rows, most recently active first.</param>
/// <param name="NextCursor">
/// What to send back as <c>after</c> for the following page, or <see langword="null"/> when this
/// page was the last. It is <see cref="CallCursor"/>'s value, passed through unread.
/// </param>
public sealed record ThreadPage(IReadOnlyList<ThreadSummary> Threads, string? NextCursor);

/// <summary>
/// The answer to a thread's creation, in the shape assistant-ui's <c>initialize</c> returns.
/// </summary>
/// <param name="RemoteId">
/// The call id. It is the same string the chat endpoint knows as <c>X-AgentCore-Session</c>, on
/// purpose: two ids for one conversation is two ids to keep in step forever.
/// </param>
/// <param name="ExternalId">A consumer's own id for the call. Nothing sets one yet.</param>
public sealed record ThreadCreated(string RemoteId, string? ExternalId);

/// <summary>
/// What the host keeps in the <c>custom</c> column, which is not what the browser put there.
/// </summary>
/// <param name="Owner">The <see cref="CallerPrincipal"/> key allowed to see this call.</param>
/// <param name="App">The browser's own fields, or <see langword="null"/> when it has none.</param>
public sealed record ThreadEnvelope(
    string Owner,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? App)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Builds the value to store, detached from the request that carried it.</summary>
    /// <param name="owner">The key that may see the call.</param>
    /// <param name="app">The browser's own fields, or <see langword="null"/> to clear them.</param>
    /// <returns>A standalone element, safe to hold after the request body is gone.</returns>
    public static JsonElement Build(string owner, JsonElement? app)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return JsonSerializer.SerializeToElement(new ThreadEnvelope(owner, app), Json);
    }

    /// <summary>Reads who owns a call.</summary>
    /// <param name="custom">The stored column, or <see langword="null"/>.</param>
    /// <returns>The owner's key, or <see langword="null"/> when the row names none.</returns>
    public static string? OwnerOf(JsonElement? custom)
        => Read(custom, nameof(Owner)) is { ValueKind: JsonValueKind.String } owner
            ? owner.GetString()
            : null;

    /// <summary>Reads the browser's own fields back out.</summary>
    /// <param name="custom">The stored column, or <see langword="null"/>.</param>
    /// <returns>What the browser wrote, or <see langword="null"/> when it wrote nothing.</returns>
    public static JsonElement? AppOf(JsonElement? custom)
        => Read(custom, nameof(App)) is { ValueKind: not JsonValueKind.Null } app ? app : null;

    private static JsonElement? Read(JsonElement? custom, string name)
    {
        if (custom is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        return element.TryGetProperty(JsonNamingPolicy.CamelCase.ConvertName(name), out var value)
            ? value
            : null;
    }
}
