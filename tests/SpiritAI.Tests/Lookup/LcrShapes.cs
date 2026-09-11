using System.Text.Json;

using AgentCore.Application.State;

using SpiritAI.Lookup;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// The real LCR shapes, as the live systems return them.
/// </summary>
/// <remarks>
/// Six model years in the knowledge base, six rows in the parts database, and the two sets do not
/// line up: the database names a year for one row only. <see cref="ModelIndex"/> is judged against
/// these shapes, so they live in their own file rather than inside its test file.
/// </remarks>
internal static class LcrShapes
{
    /// <summary>Every model slug the knowledge base publishes.</summary>
    internal static readonly string[] Slugs =
    [
        "lcr-2011", "lcr-2013", "lcr-2016", "lcr-2019", "lcr-2023", "lcr-2026",
        "srvo",
        "mt200-2022",
        "f63-2013", "f63-2015", "f63-2016", "f63-2019",
    ];

    /// <summary>The years the manuals cover the LCR in.</summary>
    internal static readonly int[] LcrYears = [2011, 2013, 2016, 2019, 2023, 2026];

    /// <summary>
    /// What each machine's cards carry at <c>facets.model_number</c>.
    /// </summary>
    /// <remarks>
    /// The F63 is deliberately absent: a machine the knowledge base documents whose cards carry no
    /// number at all is the state of 36 of the 197 machines, not an edge case. The LCR 2011 is
    /// present and carries none, which must read the same way. The MT200 2022 carries two, which is
    /// the real shape of the three machines the collection cannot settle to one SKU.
    /// </remarks>
    private static readonly Dictionary<string, string[]> Table = new(StringComparer.Ordinal)
    {
        ["lcr-2011"] = [],
        ["lcr-2013"] = ["522112"],
        ["lcr-2016"] = ["522199"],
        ["lcr-2019"] = ["522118"],
        ["lcr-2023"] = ["522122"],
        ["lcr-2026"] = ["522126"],
        ["srvo"] = ["520516"],
        ["mt200-2022"] = ["720080", "720087"],
    };

    /// <summary>The vocabulary cache a filled <c>model</c> slot leaves behind.</summary>
    internal static VocabularyCache Vocabulary()
    {
        VocabularyCache cache = new();
        cache.Replace("model", Slugs, 2000);
        return cache;
    }

    /// <summary>The knowledge base, answering a read of <c>facets.model</c>.</summary>
    internal static FacetCards Cards() => new(Table);

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
}
