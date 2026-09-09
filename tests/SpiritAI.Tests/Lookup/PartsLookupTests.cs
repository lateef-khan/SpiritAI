using System.Text.Json;

using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// Picking the right model number before asking for parts.
/// </summary>
/// <remarks>
/// These are the real shapes the F63 returns. Nine model numbers, four of them naming a year, and
/// parts lists that range from none to hundreds. The behaviour worth pinning is that a bare
/// product name never returns parts: it returns the years, so the caller asks a question a person
/// can answer instead of reading out a serial number.
/// </remarks>
public sealed class PartsLookupTests
{
    [Fact]
    public async Task AsksForTheYearWhenOnlyTheProductNameIsKnown()
    {
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("F63", null, null, null, "motor", TestContext.Current.CancellationToken);

        Assert.Equal("needs_year", answer.Outcome);
        Assert.Equal([2013, 2015, 2016, 2019], answer.Years);
        Assert.Empty(answer.Parts);
    }

    [Fact]
    public async Task NeverReturnsThePlaceholderModelsThinList()
    {
        // 563286 is named "F63" with no year and lists five parts. Answering from it looks like an
        // answer and is not, so a bare name must not reach it.
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("F63", null, null, null, "motor", TestContext.Current.CancellationToken);

        Assert.NotEqual("563286", answer.ModelNo);
    }

    [Fact]
    public async Task FindsThePartsOnceTheYearIsKnown()
    {
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("F63", 2016, null, null, "motor", TestContext.Current.CancellationToken);

        Assert.Equal("parts", answer.Outcome);
        Assert.Equal("563816", answer.ModelNo);
        Assert.Equal(8, answer.TotalRows);
        Assert.Contains(answer.Parts, part => part.SpNo == "CRG080601A-01");
    }

    [Fact]
    public async Task TakesTheModelNumberOutOfASerial()
    {
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync(null, null, "5638160000000001", null, "motor", TestContext.Current.CancellationToken);

        Assert.Equal("parts", answer.Outcome);
        Assert.Equal("563816", answer.ModelNo);
    }

    [Fact]
    public async Task SendsOneWordToSearchAndNeverAPhrase()
    {
        string? sent = null;
        var lookup = new PartsLookup(Fake(seen: arguments =>
        {
            if (arguments.TryGetValue("Search", out var value))
            {
                sent = value as string;
            }
        }));

        await lookup.FindAsync("F63", 2016, null, null, "motor belt roller", TestContext.Current.CancellationToken);

        Assert.Equal("motor", sent);
    }

    [Fact]
    public async Task SaysSoWhenTheYearWasNeverBuilt()
    {
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("F63", 1998, null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal("needs_year", answer.Outcome);
        Assert.Contains("1998", answer.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsAnEmptyListRatherThanPretendingItAsked()
    {
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("F63", 2019, null, null, "bearing", TestContext.Current.CancellationToken);

        Assert.Equal("no_parts", answer.Outcome);
        Assert.Equal("563818", answer.ModelNo);
    }

    [Fact]
    public async Task TheDatabaseAloneJustifiesOnlyTwoLcrYears()
    {
        // 2013 is in a description and 2019 is in a name. The other four rows record no year at
        // all, which is exactly why the database is not the authority on which years exist.
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("LCR", null, null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal("needs_year", answer.Outcome);
        Assert.Equal([2013, 2019], answer.Years);
    }

    [Fact]
    public async Task SaysTheProductIsUnknownWhenNothingMatches()
    {
        var lookup = new PartsLookup(Fake());

        var answer = await lookup.FindAsync("ZZ999", null, null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal("unknown_product", answer.Outcome);
    }

    /// <summary>A stand-in for DAB, holding the shapes the F63 really returns.</summary>
    private static ToolInvoker Fake(Action<IReadOnlyDictionary<string, object?>>? seen = null)
        => (toolId, arguments, _) =>
        {
            seen?.Invoke(arguments);

            var json = toolId switch
            {
                "find_model" => FindModel(arguments),
                "search_parts" => SearchParts(arguments),
                _ => "{\"status\":\"error\"}",
            };

            return ValueTask.FromResult(JsonDocument.Parse(json).RootElement.Clone());
        };

    private static string FindModel(IReadOnlyDictionary<string, object?> arguments)
    {
        if (arguments.TryGetValue("ModelNo", out var exact))
        {
            return Rows($$"""{"ModelNo":"{{exact}}","ModelName":"SOLE F63 2016"}""");
        }

        if (arguments.TryGetValue("Name", out var name) && (string?)name == "LCR")
        {
            return Rows(
                """{"ModelNo":"522110","ModelName":"LCR","ModelDesc":"FG, SOLE,  TREADMILL LCR"}""",
                """{"ModelNo":"522112","ModelName":"LCR","ModelDesc":"FG, SOLE,  TREADMILL LCR 2013"}""",
                """{"ModelNo":"522116","ModelName":"LCR","ModelDesc":"FG, SOLE,  LCR Bike Light Commercial"}""",
                """{"ModelNo":"522118","ModelName":"Sole LCR 2019","ModelDesc":"Bike Sole LCR 2019"}""",
                """{"ModelNo":"522122","ModelName":"LCR","ModelDesc":"FG, Sole LCR Bike"}""",
                """{"ModelNo":"522126","ModelName":"LCR","ModelDesc":"SOLE, LCR BIKE"}""");
        }

        return (string?)name == "F63"
            ? Rows(
                """{"ModelNo":"563286","ModelName":"F63"}""",
                """{"ModelNo":"563812","ModelName":"SOLE F63 2013"}""",
                """{"ModelNo":"563814","ModelName":"SOLE F63 2015"}""",
                """{"ModelNo":"563816","ModelName":"SOLE F63 2016"}""",
                """{"ModelNo":"563818","ModelName":"Sole F63 2019"}""",
                """{"ModelNo":"563822","ModelName":"F63"}""")
            : Rows();
    }

    private static string SearchParts(IReadOnlyDictionary<string, object?> arguments)
    {
        var model = arguments.TryGetValue("ModelNo", out var value) ? (string?)value : null;
        var search = arguments.TryGetValue("Search", out var word) ? (string?)word : null;

        // 563818 carries a list, but nothing in it is a bearing.
        if (model == "563818" && search == "bearing")
        {
            return Rows();
        }

        return model is "563816" or "563818"
            ? Rows(
                """{"SpNo":"CRG080601A-01","Description":"Drive Motor-WA252","Qty":1,"TotalRows":8}""",
                """{"SpNo":"CRD020109-04","Description":"Motor Controller w/Bracket","Qty":1,"TotalRows":8}""")
            : Rows();
    }

    private static string Rows(params string[] rows)
        => "{\"status\":\"success\",\"value\":{\"value\":["
            + string.Join(",", rows)
            + "]}}";
}
