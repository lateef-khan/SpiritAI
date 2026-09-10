using System.Globalization;
using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Reads one typed column out of a DAB row.
/// </summary>
/// <remarks>
/// A row arrives as loose JSON, so every reader of these tools meets the same four questions: is
/// the column there, is it the kind it should be, and is an empty string a value or an absence. The
/// answers live once here rather than in each of them, beside <see cref="DabEnvelope"/>, which
/// answers the question one step earlier.
/// </remarks>
internal static class DabRow
{
    /// <summary>Reads a string column, treating an empty one as absent.</summary>
    /// <param name="row">The row.</param>
    /// <param name="name">The column.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    internal static string? Text(JsonElement row, string name)
        => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() is { Length: > 0 } text ? text : null
            : null;

    /// <summary>Reads a whole-number column.</summary>
    /// <param name="row">The row.</param>
    /// <param name="name">The column.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    internal static int? Number(JsonElement row, string name)
        => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
            ? number
            : null;

    /// <summary>Reads a boolean column.</summary>
    /// <param name="row">The row.</param>
    /// <param name="name">The column.</param>
    /// <returns>The flag, or <see langword="null"/>.</returns>
    internal static bool? Flag(JsonElement row, string name)
        => row.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads a date column.
    /// </summary>
    /// <remarks>
    /// The database holds these with no time zone on them, so a zone has to be assumed to make a
    /// <see cref="DateTimeOffset" /> at all. UTC is assumed, which keeps every row consistent with
    /// every other; treat the time of day as indicative rather than exact.
    /// </remarks>
    /// <param name="row">The row.</param>
    /// <param name="name">The column.</param>
    /// <returns>The moment, or <see langword="null"/> when the column holds no date.</returns>
    internal static DateTimeOffset? Moment(JsonElement row, string name)
        => Text(row, name) is { } text
            && DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : null;
}
