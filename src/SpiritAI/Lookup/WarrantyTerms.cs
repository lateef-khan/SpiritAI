using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Turns a model's warranty row into the terms the panel lists.
/// </summary>
internal static class WarrantyTerms
{
    /// <summary>Counts each of the model's warranty periods forward from the purchase date.</summary>
    /// <remarks>
    /// The periods are held in days, one column per category, and a model carries a row per version.
    /// A machine with no purchase date still lists what it is entitled to; it just cannot say when
    /// any of it runs out.
    /// </remarks>
    /// <param name="rows">The <c>ModelWarranty</c> rows for this model, or nothing.</param>
    /// <param name="version">Which revision this serial falls in.</param>
    /// <param name="purchased">When the machine was bought.</param>
    /// <returns>The terms, or <see langword="null"/> when the table could not be read.</returns>
    internal static IReadOnlyList<WarrantyTerm>? Of(
        IReadOnlyList<JsonElement>? rows,
        int? version,
        DateTimeOffset? purchased)
    {
        if (rows is null)
        {
            return null;
        }

        var row = rows.FirstOrDefault(r => version is null || DabRow.Number(r, "Version") == version);

        if (row.ValueKind != JsonValueKind.Object)
        {
            row = rows.Count > 0 ? rows[^1] : default;
        }

        if (row.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var today = DateTimeOffset.UtcNow;

        return
        [
            .. new[]
            {
                ("Labor", "LaborPeriod"),
                ("Parts", "Part2Period"),
                ("Wear parts", "Part1Period"),
                ("Frame", "Part3Period"),
                ("Deck", "Deck"),
                ("Motor", "Motor"),
                ("Electronics", "Electronics"),
                ("Console", "Console"),
            }
            .Select(term => (term.Item1, Days: DabRow.Number(row, term.Item2)))
            .Where(term => term.Days is > 0)
            .Select(term =>
            {
                var expires = purchased?.AddDays(term.Days!.Value);

                return new WarrantyTerm(term.Item1, term.Days!.Value, expires, expires is { } end ? end > today : null);
            }),
        ];
    }
}
