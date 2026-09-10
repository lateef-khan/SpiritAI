using System.Text.Json;

using AgentCore.Application.Ports;
using AgentCore.Application.State;
using AgentCore.Domain.Knowledge;

using SpiritAI.Lookup;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// The real LCR shapes, as the live systems return them.
/// </summary>
/// <remarks>
/// Six model years in the knowledge base, six rows in the parts database, and the two sets do not
/// line up: the database names a year for one row. Both <see cref="ModelIndex"/> and
/// <see cref="PartsLookup"/> are judged against the same shapes, so they live here rather than in
/// either test file.
/// </remarks>
internal static class LcrShapes
{
    /// <summary>Every model slug the knowledge base publishes.</summary>
    internal static readonly string[] Slugs =
    [
        "lcr-2011", "lcr-2013", "lcr-2016", "lcr-2019", "lcr-2023", "lcr-2026",
        "srvo",
        "f63-2013", "f63-2015", "f63-2016", "f63-2019",
    ];

    /// <summary>The years the manuals cover the LCR in.</summary>
    internal static readonly int[] LcrYears = [2011, 2013, 2016, 2019, 2023, 2026];

    /// <summary>The vocabulary cache a filled <c>model</c> slot leaves behind.</summary>
    internal static VocabularyCache Vocabulary()
    {
        VocabularyCache cache = new();
        cache.Replace("model", Slugs, 2000);
        return cache;
    }

    /// <summary>Reads cards by facet, over the cards these six years actually hold.</summary>
    internal static IKnowledgeFacetReadPort FacetRead() => new FakeFacetRead();

    /// <summary>A facet read that refuses, as an unreachable knowledge base does.</summary>
    internal static IKnowledgeFacetReadPort RefusingFacetRead() => new ThrowingFacetRead();

    /// <summary>Answers <c>find_model</c> with the rows the live database returns.</summary>
    internal static ToolInvoker Invoker() => (toolId, arguments, _) =>
    {
        var json = toolId == "find_model" ? FindModel(arguments) : """{"status":"error"}""";

        return ValueTask.FromResult(JsonDocument.Parse(json).RootElement.Clone());
    };

    private static string FindModel(IReadOnlyDictionary<string, object?> arguments)
    {
        arguments.TryGetValue("Name", out var name);

        return ((string?)name)?.ToUpperInvariant() switch
        {
            "LCR" => Rows(
                """{"ModelNo":"522110","ModelName":"LCR","ModelDesc":"FG, SOLE,  TREADMILL LCR"}""",
                """{"ModelNo":"522112","ModelName":"LCR","ModelDesc":"FG, SOLE,  TREADMILL LCR 2013"}""",
                """{"ModelNo":"522116","ModelName":"LCR","ModelDesc":"FG, SOLE,  LCR Bike Light Commercial"}""",
                """{"ModelNo":"522118","ModelName":"Sole LCR 2019","ModelDesc":"Bike Sole LCR 2019"}""",
                """{"ModelNo":"522122","ModelName":"LCR","ModelDesc":"FG, Sole LCR Bike"}""",
                """{"ModelNo":"522126","ModelName":"LCR","ModelDesc":"SOLE, LCR BIKE"}"""),
            "SRVO" => Rows("""{"ModelNo":"520516","ModelName":"SRVO","ModelDesc":"FG, SOLE, SRVO"}"""),
            _ => Rows(),
        };
    }

    /// <summary>The envelope a DAB stored procedure wraps its rows in.</summary>
    private static string Rows(params string[] rows)
        => "{\"status\":\"success\",\"value\":{\"value\":[" + string.Join(",", rows) + "]}}";

    private static KnowledgeCard Card(string cardId, string text)
        => new() { CardId = cardId, Text = text, ViaLink = false };

    /// <summary>The cards each model year holds, keyed by slug.</summary>
    private sealed class FakeFacetRead : IKnowledgeFacetReadPort
    {
        public ValueTask<IReadOnlyList<KnowledgeCard>> ReadByFacetAsync(
            string path, string value, int limit, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<KnowledgeCard> cards = path != "facets.model"
                ? []
                : value switch
                {
                    // One card of the year states the number, and it says so in its own id.
                    "lcr-2023" =>
                    [
                        Card("lcr-2023-belt-tension", "Tension the belt to 40 turns."),
                        Card("lcr-2023-model-overview", "The LCR 2023 is model number 522122."),
                    ],

                    // A year the manuals cover and no card gives a number for.
                    "lcr-2011" => [Card("lcr-2011-console-modes", "The console has four modes.")],

                    // 522199 is not one of the six rows find_model returns for the LCR.
                    "lcr-2016" => [Card("lcr-2016-model-overview", "The LCR 2016 is model number 522199.")],

                    "srvo" => [Card("srvo-model-overview", "The SRVO is model number 520516.")],
                    _ => [],
                };

            return ValueTask.FromResult(cards);
        }
    }

    private sealed class ThrowingFacetRead : IKnowledgeFacetReadPort
    {
        public ValueTask<IReadOnlyList<KnowledgeCard>> ReadByFacetAsync(
            string path, string value, int limit, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("the knowledge base is unreachable.");
    }
}
