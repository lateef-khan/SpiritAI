using SpiritAI.Handoffs.Store;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// The query string of a listing, read into what the store takes.
/// </summary>
/// <param name="Filter">Which rows, whose, and from which end.</param>
/// <param name="After">Where the previous page ended, or <see langword="null"/> for the first.</param>
public sealed record HandoffListQuery(HandoffFilter Filter, HandoffCursor? After)
{
    /// <summary>Reads the query string, or says which part could not be read.</summary>
    /// <param name="view"><c>open</c>, <c>waiting</c>, or <c>done</c>; open when absent.</param>
    /// <param name="owner"><c>me</c> or <c>none</c>; everyone's rows when absent.</param>
    /// <param name="order"><c>oldest</c> or <c>newest</c>; oldest first for the open views, newest first for done, when absent.</param>
    /// <param name="cursor">A <c>nextCursor</c> this host handed out, or nothing for the first page.</param>
    /// <param name="staffKey">The caller, for <paramref name="owner"/> of <c>me</c>.</param>
    /// <param name="query">What was read, when everything could be.</param>
    /// <param name="problem">Why not, otherwise.</param>
    /// <returns>Whether every part could be read.</returns>
    public static bool TryRead(
        string? view,
        string? owner,
        string? order,
        string? cursor,
        string staffKey,
        out HandoffListQuery? query,
        out string problem)
    {
        query = null;
        problem = "";

        if (!TryReadView(view, out var readView))
        {
            problem = $"'{view}' is not a view. Send 'open', 'waiting', 'done', or nothing at all.";
            return false;
        }

        var readOwner = owner switch
        {
            null or "" => HandoffOwner.Anyone,
            "me" => HandoffOwner.Me,
            "none" => HandoffOwner.Nobody,
            _ => (HandoffOwner?)null,
        };

        if (readOwner is null)
        {
            problem = $"'{owner}' is not an owner. Send 'me', 'none', or nothing at all.";
            return false;
        }

        var readOrder = order switch
        {
            null or "" => readView == HandoffView.Done ? HandoffOrder.NewestFirst : HandoffOrder.OldestFirst,
            "oldest" => HandoffOrder.OldestFirst,
            "newest" => HandoffOrder.NewestFirst,
            _ => (HandoffOrder?)null,
        };

        if (readOrder is null)
        {
            problem = $"'{order}' is not an order. Send 'oldest', 'newest', or nothing at all.";
            return false;
        }

        HandoffCursor? after = null;

        if (!string.IsNullOrEmpty(cursor))
        {
            if (!HandoffCursor.TryParse(cursor, out var parsed))
            {
                problem = "cursor is not one this host handed out. Send the nextCursor of the page before.";
                return false;
            }

            after = parsed;
        }

        query = new HandoffListQuery(new HandoffFilter(readView, readOrder.Value, readOwner.Value, staffKey), after);
        return true;
    }

    /// <summary>Reads the wire's spelling of a view.</summary>
    /// <param name="text">What the caller sent, or nothing.</param>
    /// <param name="view">The view it named; open when it named none.</param>
    /// <returns>Whether the text was a view.</returns>
    public static bool TryReadView(string? text, out HandoffView view)
    {
        view = HandoffView.Open;

        return string.IsNullOrEmpty(text)
            || (Enum.TryParse(text, ignoreCase: true, out view) && Enum.IsDefined(view));
    }
}
