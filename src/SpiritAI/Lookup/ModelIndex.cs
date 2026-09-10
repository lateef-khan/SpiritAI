using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using AgentCore.Application.Ports;
using AgentCore.Application.State;

namespace SpiritAI.Lookup;

/// <summary>
/// Turns a product name and a year into one model number, out of the manuals.
/// </summary>
/// <param name="cards">
/// Reads cards by facet, or <see langword="null"/> when the knowledge base serves no such
/// capability. The caller asks the knowledge port for it; a store that cannot filter answers none.
/// </param>
/// <param name="vocabulary">The cache the <c>model</c> slot's refresh writes into.</param>
/// <param name="invoke">The seam that calls one DAB tool.</param>
public sealed partial class ModelIndex(
    IKnowledgeFacetReadPort? cards,
    VocabularyCache vocabulary,
    ToolInvoker invoke)
{
    /// <summary>The payload path the model slug is stored at.</summary>
    private const string FacetPath = "facets.model";

    /// <summary>The state slot whose vocabulary carries every slug.</summary>
    private const string Slot = "model";

    /// <summary>The tool id <c>spirit.yaml</c> aliases the model reader under.</summary>
    private const string FindModel = "find_model";

    /// <summary>How many cards one model year can answer with.</summary>
    private const int Cap = 50;

    private readonly VocabularyCache _vocabulary = vocabulary;
    private readonly ToolInvoker _invoke = invoke;

    [GeneratedRegex(@"(?<!\d)\d{6}(?!\d)")]
    private static partial Regex SixDigits { get; }

    [GeneratedRegex("-(?<year>(?:19|20)[0-9]{2})(?:-ac)?$")]
    private static partial Regex TrailingYear { get; }

    /// <summary>Finds the model number one product name and year point at.</summary>
    /// <param name="productName">A product name such as <c>LCR</c>.</param>
    /// <param name="year">The year the machine was built, or nothing.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The model, or the years still needed to name one.</returns>
    public async Task<ModelAnswer> FindAsync(
        string? productName,
        int? year,
        CancellationToken cancellationToken = default)
    {
        if (productName is not { Length: > 0 } name)
        {
            return new ModelAnswer("unknown_product", null, null, [], "No product name was given.");
        }

        var wanted = name.Trim().ToLowerInvariant();
        var slugs = SlugsOf(wanted);

        if (slugs.Count == 0)
        {
            return new ModelAnswer(
                "unknown_product", null, null, [], $"No machine named '{name}' is documented.");
        }

        var years = Years(slugs);

        if (Pick(slugs, years, year) is not { } slug)
        {
            return new ModelAnswer(
                "needs_year",
                null,
                null,
                years,
                year is { } asked
                    ? $"'{name}' has no {asked} version. It was built in {string.Join(", ", years)}."
                    : $"'{name}' was built in {string.Join(", ", years)}, and their parts differ.");
        }

        var stated = await StatedModelNoAsync(slug, cancellationToken).ConfigureAwait(false);

        if (stated is null)
        {
            return new ModelAnswer(
                "no_record", slug, null, years, $"No document states a model number for {slug}.");
        }

        return await CarriedByTheDatabaseAsync(wanted, stated, cancellationToken).ConfigureAwait(false)
            ? new ModelAnswer("model", slug, stated, years, $"{slug} is model {stated}.")
            : new ModelAnswer(
                "no_record",
                slug,
                null,
                years,
                $"The documents give {slug} model number {stated}, which the parts records do not "
                + "carry. Treat it as unconfirmed.");
    }

    /// <summary>Every slug the vocabulary holds for one product.</summary>
    private IReadOnlyList<string> SlugsOf(string product)
        => _vocabulary.Snapshot().TryGetValue(Slot, out var view)
            ? [.. view.Originals
                .Select(slug => slug.ToLowerInvariant())
                .Where(slug => string.Equals(ProductOf(slug), product, StringComparison.Ordinal))]
            : [];

    /// <summary>The product a slug names, with its year taken off.</summary>
    private static string ProductOf(string slug) => TrailingYear.Replace(slug, string.Empty);

    /// <summary>The year a slug names, or nothing.</summary>
    private static int? YearOf(string slug)
        => TrailingYear.Match(slug) is { Success: true } match
            ? int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture)
            : null;

    /// <summary>The years these slugs offer, in order and without repeats.</summary>
    private static IReadOnlyList<int> Years(IReadOnlyList<string> slugs)
        => [.. slugs.Select(YearOf).OfType<int>().Distinct().Order()];

    /// <summary>The one slug a year names, or the only slug when the product has one.</summary>
    private static string? Pick(IReadOnlyList<string> slugs, IReadOnlyList<int> years, int? year)
    {
        if (year is { } asked)
        {
            return slugs.FirstOrDefault(slug => YearOf(slug) == asked);
        }

        return slugs.Count == 1 && years.Count == 0 ? slugs[0] : null;
    }

    /// <summary>The model number the cards of one slug state, or nothing.</summary>
    private async Task<string?> StatedModelNoAsync(string slug, CancellationToken cancellationToken)
    {
        if (cards is null)
        {
            return null;
        }

        IReadOnlyList<AgentCore.Domain.Knowledge.KnowledgeCard> found;
        try
        {
            found = await cards.ReadByFacetAsync(FacetPath, slug, Cap, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A knowledge base that refuses is a model number nobody stated. The caller then asks
            // for the year, which is the answer it would have given anyway.
            return null;
        }

        // A card written to state the model number says so in its own id. Its six digit number is
        // the model number; a six digit number anywhere else is as likely to be a part.
        var numbers = found
            .Where(card => card.CardId.EndsWith("-model-overview", StringComparison.Ordinal)
                || card.CardId.EndsWith("-model-number", StringComparison.Ordinal))
            .SelectMany(card => SixDigits.Matches(card.Text).Select(match => match.Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Several numbers on one overview card is a card that covers more than one machine. Naming
        // either would be a guess.
        return numbers.Count == 1 ? numbers[0] : null;
    }

    /// <summary>Whether the parts records carry one model number for one product.</summary>
    private async Task<bool> CarriedByTheDatabaseAsync(
        string product, string modelNo, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await _invoke(
                    FindModel,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["Name"] = product,
                        ["Top"] = 100,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return DabEnvelope.RowsOf(answer)?
                .Any(row => row.TryGetProperty("ModelNo", out var value)
                    && value.ValueKind == JsonValueKind.String
                    && string.Equals(value.GetString(), modelNo, StringComparison.Ordinal))
                ?? false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return false;
        }
    }
}
