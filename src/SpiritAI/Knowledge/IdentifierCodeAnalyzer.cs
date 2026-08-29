using System.Text.RegularExpressions;

using AgentCore.Application.Knowledge;

namespace SpiritAI.Knowledge;

/// <summary>
/// Treats a short letter run followed by a short digit run as a code the answer must contain.
/// </summary>
/// <remarks>
/// <para>
/// This used to ship inside AgentCore. It does not belong there: it is a claim about the words
/// Spirit's own documents use, not about knowledge bases in general. AgentCore ships one analyzer,
/// <c>none</c>, which requires nothing. Every rule beyond that is the consumer's, so this is the
/// consumer's.
/// </para>
/// <para>
/// The rule is 1 to 4 letters then 1 to 3 digits, lowercased. It catches console fault codes
/// (<c>e33</c>, <c>ol1</c>, <c>ce10</c>) and product lines (<c>ct900</c>). It deliberately does not
/// catch a 16 digit serial number or a 6 digit model number: those carry no letters, and making a
/// bare number mandatory would drop every card that discusses the model in words.
/// </para>
/// <para>
/// A term this returns becomes MANDATORY. A card that does not carry the word is dropped no matter
/// how well it matches otherwise, and nothing is logged when that happens. If good answers start
/// going missing, set <c>analyzer: none</c> in <c>spirit.yaml</c> and see whether they come back.
/// </para>
/// </remarks>
public sealed partial class IdentifierCodeAnalyzer : IKnowledgeQueryAnalyzer
{
    /// <summary>The name <c>providers.knowledge.analyzer</c> selects this by.</summary>
    public const string AnalyzerName = "identifier-codes";

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Word { get; }

    [GeneratedRegex("^[a-z]{1,4}[0-9]{1,3}$")]
    private static partial Regex Identifier { get; }

    /// <inheritdoc />
    public string Name => AnalyzerName;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<string> RequiredTerms(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return
        [
            .. Word.Matches(query.ToLowerInvariant())
                .Select(match => match.Value)
                .Where(token => Identifier.IsMatch(token))
                .Distinct(StringComparer.Ordinal),
        ];
    }
}
