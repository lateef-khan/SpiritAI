using System.Text.RegularExpressions;

using AgentCore.Application.Knowledge;
using AgentCore.Application.State;

namespace SpiritAI.Knowledge;

/// <summary>
/// Decides which words of a question the answer must carry.
/// </summary>
/// <param name="products">
/// The product names the knowledge base publishes, read fresh on every query so a vocabulary
/// refresh takes effect without a restart. Absent, or empty, and only the shape rules apply.
/// </param>
public sealed partial class IdentifierCodeAnalyzer(Func<IReadOnlySet<string>>? products = null)
    : IKnowledgeQueryAnalyzer
{
    /// <summary>The name <c>providers.knowledge.analyzer</c> selects this by.</summary>
    public const string AnalyzerName = "identifier-codes";

    /// <summary>The state slot whose vocabulary carries the model slugs.</summary>
    public const string ModelSlot = "model";

    private static readonly IReadOnlySet<string> NoProducts =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly Func<IReadOnlySet<string>> _products = products ?? (() => NoProducts);

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Word { get; }

    /// <summary>Letters then digits: <c>e33</c>, <c>ol1</c>, <c>ct900</c>.</summary>
    [GeneratedRegex("^[a-z]{1,4}[0-9]{1,3}$")]
    private static partial Regex LettersThenDigits { get; }

    /// <summary>Digits then letters: <c>40t</c>, <c>70t</c>, <c>80t</c>.</summary>
    [GeneratedRegex("^[0-9]{1,4}[a-z]{1,3}$")]
    private static partial Regex DigitsThenLetters { get; }

    /// <summary>Letters around digits: <c>e95s</c>, <c>sb1200</c>, <c>ct900ent</c>, <c>ctsbs900</c>.</summary>
    [GeneratedRegex("^[a-z]{1,5}[0-9]{1,4}[a-z]{0,4}$")]
    private static partial Regex LettersAroundDigits { get; }

    /// <summary>A model slug's trailing year, and the <c>-ac</c> some carry after it.</summary>
    [GeneratedRegex("-(?:19|20)[0-9]{2}(?:-ac)?$")]
    private static partial Regex TrailingYear { get; }

    /// <inheritdoc />
    public string Name => AnalyzerName;

    /// <summary>Reads the product names out of a set of model slugs.</summary>
    /// <param name="slugs">Values such as <c>lcr-2023</c>, <c>tt8-2016-ac</c> or <c>ct900</c>.</param>
    /// <returns>The product each slug names, lowercased and without repeats.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slugs"/> is <see langword="null"/>.</exception>
    public static IReadOnlySet<string> ProductsIn(IEnumerable<string> slugs)
    {
        ArgumentNullException.ThrowIfNull(slugs);

        return new HashSet<string>(
            slugs.Select(slug => TrailingYear.Replace(slug.ToLowerInvariant(), string.Empty))
                .Where(product => product.Length > 0),
            StringComparer.Ordinal);
    }

    /// <summary>Reads the product names out of the cache the vocabulary refresh writes into.</summary>
    /// <param name="vocabulary">The cache, or <see langword="null"/> when the host registered none.</param>
    /// <returns>The product names, or an empty set when the slot has never been read.</returns>
    public static IReadOnlySet<string> ProductsIn(VocabularyCache? vocabulary)
        => vocabulary?.Snapshot().TryGetValue(ModelSlot, out var view) == true
            ? ProductsIn(view.Originals)
            : NoProducts;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<string> RequiredTerms(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var known = _products();

        return
        [
            .. Word.Matches(query.ToLowerInvariant())
                .Select(match => match.Value)
                .Where(token => IsCode(token) || known.Contains(token))
                .Distinct(StringComparer.Ordinal),
        ];
    }

    private static bool IsCode(string token)
        => LettersThenDigits.IsMatch(token)
            || DigitsThenLetters.IsMatch(token)
            || LettersAroundDigits.IsMatch(token);
}
