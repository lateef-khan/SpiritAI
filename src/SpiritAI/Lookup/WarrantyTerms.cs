using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Turns the warranty rows of <c>get_unit</c> into the terms the panel lists.
/// </summary>
internal static class WarrantyTerms
{
    /// <summary>Reads each warranty term the database worked out for one machine.</summary>
    /// <param name="rows">The <c>get_unit</c> rows, or nothing.</param>
    /// <returns>The terms, or <see langword="null"/> when the tool could not be read.</returns>
    internal static IReadOnlyList<WarrantyTerm>? Of(IReadOnlyList<JsonElement>? rows)
    {
        if (rows is null)
        {
            return null;
        }

        var terms = rows
            .Where(row => DabRow.Text(row, "Term") is not null && DabRow.Number(row, "Days") is not null)
            .ToList();

        var typed = terms.Select(row => DabRow.Text(row, "WarrantyType")).Distinct().Count() > 1;

        return
        [
            .. terms.Select(row =>
            {
                var term = DabRow.Text(row, "Term")!;
                var type = DabRow.Text(row, "WarrantyType");
                var lifetime = DabRow.Flag(row, "Lifetime") is true;

                return new WarrantyTerm(
                    typed && type is not null ? $"{term} ({type})" : term,
                    DabRow.Number(row, "Days")!.Value,
                    lifetime ? null : DabRow.Moment(row, "Expires"),
                    DabRow.Flag(row, "InWarranty"));
            }),
        ];
    }
}
