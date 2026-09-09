using System.Globalization;
using System.Text.RegularExpressions;

namespace SpiritAI.Lookup;

/// <summary>
/// One model number, and the build year its name carries.
/// </summary>
/// <remarks>
/// A product name covers several model numbers, one for each year the machine was built, and their
/// parts lists differ. The year is the only thing that tells them apart that a person standing at
/// the machine can actually answer, and the database writes it into the model's own name.
/// </remarks>
/// <param name="ModelNo">The six digit model number.</param>
/// <param name="Name">The model's name, as the database holds it.</param>
/// <param name="Year">The year the name carries, or <see langword="null"/> when it carries none.</param>
public sealed partial record ModelYear(string ModelNo, string Name, int? Year)
{
    /// <summary>The earliest year a name is read as naming, and the latest.</summary>
    private const int Earliest = 1990;

    private const int Latest = 2099;

    /// <summary>Reads one model's name.</summary>
    /// <param name="modelNo">The six digit model number.</param>
    /// <param name="name">The model's name, such as <c>SOLE F63 2016</c>.</param>
    /// <returns>The model number and the year its name carries.</returns>
    public static ModelYear Read(string modelNo, string? name)
    {
        var text = name ?? string.Empty;
        int? year = null;

        foreach (Match match in FourDigits().Matches(text))
        {
            var candidate = int.Parse(match.Value, CultureInfo.InvariantCulture);

            // The last year wins. A name puts the year at the end, and anything earlier in the
            // string that looks like one is part of the product's own name.
            if (candidate is >= Earliest and <= Latest)
            {
                year = candidate;
            }
        }

        return new ModelYear(modelNo, text, year);
    }

    /// <summary>Picks the one model whose name carries a year.</summary>
    /// <param name="models">Every model one product name covers.</param>
    /// <param name="year">The year the person gave.</param>
    /// <returns>That year's model, or <see langword="null"/> when no model carries it.</returns>
    public static ModelYear? Pick(IReadOnlyList<ModelYear> models, int year)
    {
        ArgumentNullException.ThrowIfNull(models);

        return models.FirstOrDefault(model => model.Year == year);
    }

    /// <summary>Lists the years these models offer, in order and without repeats.</summary>
    /// <param name="models">Every model one product name covers.</param>
    /// <returns>The years a person may be asked to choose between.</returns>
    public static IReadOnlyList<int> Years(IReadOnlyList<ModelYear> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        return [.. models.Where(model => model.Year is not null)
            .Select(model => model.Year!.Value)
            .Distinct()
            .Order()];
    }

    /// <summary>A run of exactly four digits, bounded so a longer number never matches.</summary>
    [GeneratedRegex(@"(?<!\d)\d{4}(?!\d)")]
    private static partial Regex FourDigits();
}
