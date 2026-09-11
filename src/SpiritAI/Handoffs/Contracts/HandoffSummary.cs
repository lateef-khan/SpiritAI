using AgentCore.Application.Calls;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Contracts;

/// <summary>
/// One handoff, as the inbox lists it: the row, and what the chat behind it is about.
/// </summary>
/// <param name="Id">The row's own number.</param>
/// <param name="CallId">The chat.</param>
/// <param name="Status"><c>waiting</c>, <c>human</c>, or <c>done</c>.</param>
/// <param name="AskedBy"><c>bot</c> or <c>visitor</c>.</param>
/// <param name="Reason">What the asker said the person is for, when they said.</param>
/// <param name="AskedAt">When the ask was made.</param>
/// <param name="Assignee">Who holds the chat, once somebody does.</param>
/// <param name="ClaimedAt">When they took it.</param>
/// <param name="Email">Where a reply goes when the visitor is not there to read it.</param>
/// <param name="DoneAt">When the chat was handed back to the bot.</param>
/// <param name="Title">The chat's title, when the call has one.</param>
/// <param name="FirstLine">The first thing the visitor said, so the queue reads at a glance.</param>
/// <param name="Position">Where a waiting chat stands in the line, one for the front. Absent otherwise.</param>
public sealed record HandoffSummary(
    long Id,
    string CallId,
    string Status,
    string AskedBy,
    string? Reason,
    DateTimeOffset AskedAt,
    HandoffAssignee? Assignee,
    DateTimeOffset? ClaimedAt,
    string? Email,
    DateTimeOffset? DoneAt,
    string? Title,
    string? FirstLine,
    int? Position)
{
    /// <summary>Describes one row to the inbox.</summary>
    /// <param name="row">The row.</param>
    /// <param name="call">The chat's own row, or <see langword="null"/> when store 0 holds none.</param>
    /// <param name="firstLine">The first thing the visitor said, or <see langword="null"/> when nothing yet.</param>
    /// <param name="position">Where a waiting row stands, or <see langword="null"/> when it is not waiting.</param>
    /// <returns>The summary.</returns>
    public static HandoffSummary Of(Handoff row, CallRecord? call, string? firstLine, int? position)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new HandoffSummary(
            row.Id,
            row.CallId,
            StatusOf(row.Status),
            row.AskedBy.ToString().ToLowerInvariant(),
            row.Reason,
            row.AskedAt,
            row is { AssigneeKey: { } key, AssigneeName: { } name } ? new HandoffAssignee(key, name) : null,
            row.ClaimedAt,
            row.Email,
            row.DoneAt,
            call?.Title,
            firstLine,
            position);
    }

    /// <summary>Spells a status the way the wire does: lowercase, the same as the table.</summary>
    /// <param name="status">The status.</param>
    /// <returns><c>waiting</c>, <c>human</c>, or <c>done</c>.</returns>
    public static string StatusOf(HandoffStatus status) => status.ToString().ToLowerInvariant();

    /// <summary>Reads the wire's spelling of a status.</summary>
    /// <param name="text">What the caller sent.</param>
    /// <param name="status">The status it named.</param>
    /// <returns><see langword="true"/> when the value was one this host knows.</returns>
    public static bool TryReadStatus(string? text, out HandoffStatus status)
    {
        foreach (var candidate in Enum.GetValues<HandoffStatus>())
        {
            if (string.Equals(text, StatusOf(candidate), StringComparison.Ordinal))
            {
                status = candidate;
                return true;
            }
        }

        status = HandoffStatus.Waiting;
        return false;
    }
}
