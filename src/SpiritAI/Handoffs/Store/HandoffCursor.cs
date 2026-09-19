using System.Buffers.Text;
using System.Globalization;
using System.Text;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Store;

/// <summary>
/// Where one page of a listing ended, so the next page starts after it.
/// </summary>
/// <remarks>
/// A page is cut on the row's sort key, not on an offset: rows that join or leave the list
/// between two reads then shift nothing, and a row is never shown twice or skipped. The key is
/// the instant the view orders by, and the id breaks ties on it.
/// </remarks>
/// <param name="At">The instant the view sorts on: the ask for the open views, the close for done.</param>
/// <param name="Id">The row's own number, for rows sharing an instant.</param>
public readonly record struct HandoffCursor(DateTimeOffset At, long Id)
{
    /// <summary>The cursor that follows a row, for the view it was listed in.</summary>
    /// <param name="row">The last row of a page.</param>
    /// <param name="view">The view that page came from.</param>
    /// <returns>The cursor to hand back for the next page.</returns>
    public static HandoffCursor After(Handoff row, HandoffView view)
    {
        ArgumentNullException.ThrowIfNull(row);

        var at = view == HandoffView.Done
            ? row.DoneAt ?? throw new ArgumentException("A done row has no close.", nameof(row))
            : row.AskedAt;

        return new HandoffCursor(at, row.Id);
    }

    /// <summary>Spells the cursor for the wire. Opaque to the caller; only this type reads it.</summary>
    /// <returns>A short URL-safe string.</returns>
    public string Encode()
        => Base64Url.EncodeToString(
            Encoding.ASCII.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"{At.UtcTicks}:{Id}")));

    /// <summary>Reads a cursor <see cref="Encode"/> spelled.</summary>
    /// <param name="text">What the caller sent.</param>
    /// <param name="cursor">The cursor it named.</param>
    /// <returns><see langword="false"/> when the text is not one this host wrote.</returns>
    public static bool TryParse(string? text, out HandoffCursor cursor)
    {
        cursor = default;

        if (string.IsNullOrEmpty(text) || !Base64Url.IsValid(text))
        {
            return false;
        }

        var parts = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(text)).Split(':');

        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        cursor = new HandoffCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}
