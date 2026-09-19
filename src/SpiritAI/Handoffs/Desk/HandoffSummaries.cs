using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Reads;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// Dresses a handoff row with what the chat behind it holds: its title, the first thing the
/// visitor said, where it stands in the line, and whether the reader has seen all of it. The
/// inbox lists these, and a new ask is pushed to staff as one.
/// </summary>
internal static class HandoffSummaries
{
    /// <summary>Summarises every row of a listing, for one reader.</summary>
    /// <remarks>
    /// Three reads per row, one row at a time, and one read of the reader's marks for the lot.
    /// The queue is tens of rows at most, so the N+1 is cheaper than a join this host has no way
    /// to write: the rows live in two schemas behind two ports.
    /// </remarks>
    /// <param name="store">Where the position is read.</param>
    /// <param name="conversations">Where the title and the words are read.</param>
    /// <param name="reads">Where the reader's marks are read.</param>
    /// <param name="staffKey">The reader.</param>
    /// <param name="rows">The rows, in the order they are to be listed.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One summary per row, in the same order.</returns>
    public static async Task<IReadOnlyList<HandoffSummary>> OfAsync(
        IHandoffStore store,
        IConversations conversations,
        IConversationReadStore reads,
        string staffKey,
        IReadOnlyList<Handoff> rows,
        CancellationToken cancellationToken)
    {
        var seen = await reads
            .SeenAsync(staffKey, rows.Select(row => row.ConversationId).ToArray(), cancellationToken)
            .ConfigureAwait(false);

        List<HandoffSummary> summaries = new(rows.Count);

        foreach (var row in rows)
        {
            var mark = seen.TryGetValue(row.ConversationId, out var ordinal) ? ordinal : (int?)null;
            summaries.Add(await OfAsync(store, conversations, row, mark, cancellationToken).ConfigureAwait(false));
        }

        return summaries;
    }

    /// <summary>Summarises one row for one reader.</summary>
    /// <param name="store">Where the position is read.</param>
    /// <param name="conversations">Where the title and the words are read.</param>
    /// <param name="reads">Where the reader's mark is read.</param>
    /// <param name="staffKey">The reader.</param>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summary, with a position only while the row is waiting.</returns>
    public static async Task<HandoffSummary> OfAsync(
        IHandoffStore store,
        IConversations conversations,
        IConversationReadStore reads,
        string staffKey,
        Handoff row,
        CancellationToken cancellationToken)
    {
        var seen = await reads.SeenAsync(staffKey, [row.ConversationId], cancellationToken).ConfigureAwait(false);
        var mark = seen.TryGetValue(row.ConversationId, out var ordinal) ? ordinal : (int?)null;

        return await OfAsync(store, conversations, row, mark, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Summarises one row against one mark. A push to every member of staff at once passes no
    /// mark: a fresh ask is unread until somebody opens it.
    /// </summary>
    /// <param name="store">Where the position is read.</param>
    /// <param name="conversations">Where the title and the words are read.</param>
    /// <param name="row">The row.</param>
    /// <param name="seenOrdinal">The last ordinal the reader has seen, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The summary, with a position only while the row is waiting.</returns>
    public static async Task<HandoffSummary> OfAsync(
        IHandoffStore store,
        IConversations conversations,
        Handoff row,
        int? seenOrdinal,
        CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(row.ConversationId, cancellationToken).ConfigureAwait(false);
        var words = await conversations.ReadAsync(row.ConversationId, cancellationToken).ConfigureAwait(false);

        var position = row.Status == HandoffStatus.Waiting
            ? await store.PositionAsync(row.ConversationId, cancellationToken).ConfigureAwait(false)
            : null;

        return HandoffSummary.Of(
            row, conversation, FirstLineOf(words), position, ReplyDue.Of(words), Unread.Of(words, seenOrdinal));
    }

    /// <summary>The first thing the visitor said: the text of the lowest user row.</summary>
    /// <param name="rows">Every stored message of the conversation. Order does not matter.</param>
    /// <returns>The words, or <see langword="null"/> when the visitor has said nothing yet.</returns>
    public static string? FirstLineOf(IReadOnlyList<ConversationMessage> rows)
    {
        var first = rows
            .Where(row => row.Content.Role == ChatRole.User)
            .OrderBy(row => row.Ordinal)
            .FirstOrDefault();

        if (first is null)
        {
            return null;
        }

        var text = string.Concat(first.Content.Contents.OfType<TextContent>().Select(part => part.Text));

        return text.Length > 0 ? text : null;
    }
}
