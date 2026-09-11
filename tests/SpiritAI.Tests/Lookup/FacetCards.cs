using AgentCore.Application.Ports;
using AgentCore.Domain.Knowledge;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// The knowledge base, answering a read of <c>facets.model</c> with one machine's cards.
/// </summary>
/// <remarks>
/// Each machine answers with two cards, because that is the shape the live collection returns: the
/// facet rides every card whose <c>applies_to</c> names one machine, and the cards that name
/// several omit it. The number must survive being read beside a card that states none.
/// </remarks>
/// <param name="table">Every machine the collection holds, against the numbers its cards carry.</param>
internal sealed class FacetCards(IReadOnlyDictionary<string, string[]> table) : IKnowledgeFacetReadPort
{
    /// <inheritdoc />
    public ValueTask<IReadOnlyList<KnowledgeCard>> ReadByFacetAsync(
        string path, string value, int limit, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<KnowledgeCard> cards = table.TryGetValue(value, out var numbers)
            ? [Card($"{value}-model-overview", value, numbers), Card($"{value}-safety", value, null)]
            : [];

        return ValueTask.FromResult(cards);
    }

    /// <summary>One card, carrying the facets a Spirit card carries.</summary>
    private static KnowledgeCard Card(string cardId, string slug, string[]? numbers) => new()
    {
        CardId = cardId,
        Text = string.Empty,
        ViaLink = false,
        Extras = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["facets"] = Facets(slug, numbers),
        },
    };

    /// <summary>The facet block, without <c>model_number</c> when the card states none.</summary>
    private static IReadOnlyDictionary<string, object?> Facets(string slug, string[]? numbers)
    {
        Dictionary<string, object?> facets = new(StringComparer.Ordinal) { ["model"] = slug };

        if (numbers is not null)
        {
            facets["model_number"] = (IReadOnlyList<object?>)[.. numbers];
        }

        return facets;
    }
}
