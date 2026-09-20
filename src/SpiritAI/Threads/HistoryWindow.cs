using System.Globalization;

using AgentCore.Application.Transcript;

using Microsoft.AspNetCore.Http.HttpResults;

namespace SpiritAI.Threads;

/// <summary>
/// The window of a thread's words one read asks for, off the query string. Three routes read words
/// the same way — the signed-in thread list, the public widget, and the staff desk — so the
/// spelling of a page lives once.
/// </summary>
public static class HistoryWindow
{
    /// <summary>How many turns a read answers with when the caller names no number.</summary>
    public const int DefaultTurns = 30;

    /// <summary>The most turns one read answers with, whatever the caller asked for.</summary>
    public const int MaxTurns = 100;

    /// <summary>Reads the window the query string names.</summary>
    /// <param name="query">What the caller sent. Its limit is clamped to <see cref="MaxTurns"/>.</param>
    /// <param name="window">The window, when the cursor reads.</param>
    /// <returns><see langword="false"/> when the cursor is not one this host wrote.</returns>
    public static bool TryRead(HistoryQuery query, out TranscriptWindow window)
    {
        ArgumentNullException.ThrowIfNull(query);

        int? beforeTurn = null;

        if (query.Before is { } before)
        {
            if (!int.TryParse(before, NumberStyles.None, CultureInfo.InvariantCulture, out var turn))
            {
                window = default;
                return false;
            }

            beforeTurn = turn;
        }

        window = new TranscriptWindow(beforeTurn, Math.Clamp(query.Limit ?? DefaultTurns, 1, MaxTurns));
        return true;
    }

    /// <summary>The answer to a cursor <see cref="TryRead"/> could not read.</summary>
    /// <param name="query">What the caller sent.</param>
    /// <returns>A 400 that says what to send instead.</returns>
    public static ProblemHttpResult Refuse(HistoryQuery query)
        => TypedResults.Problem(
            $"'{query?.Before}' is not a cursor. Send back the nextCursor a page answered with, or nothing at all.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "The request cannot be read.");

    /// <summary>Spells the cursor the next older page is asked for with.</summary>
    /// <param name="olderBefore">The turn the store said to read before, or <see langword="null"/> at the start.</param>
    /// <returns>The cursor, or <see langword="null"/> when there is no older page.</returns>
    public static string? CursorOf(int? olderBefore)
        => olderBefore?.ToString(CultureInfo.InvariantCulture);
}
