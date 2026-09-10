using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// Turning a product name and a year into one model number.
/// </summary>
/// <remarks>
/// Ranking cannot separate 2023 from 2026 — measured, the 2026 card wins a search for the 2023 one
/// — so every lookup here is a filter. A number the manuals state is then checked against the parts
/// database before it is returned, because a stale or mistyped card must not send a parts lookup to
/// a machine nobody built.
/// </remarks>
public sealed class ModelIndexTests
{
    [Fact]
    public async Task FindsTheModelNumberForOneYear()
    {
        var answer = await Index().FindAsync("LCR", 2023, TestContext.Current.CancellationToken);

        Assert.Equal("model", answer.Outcome);
        Assert.Equal("lcr-2023", answer.Slug);
        Assert.Equal("522122", answer.ModelNo);
    }

    [Fact]
    public async Task ListsTheYearsFromTheManualsWhenNoYearIsGiven()
    {
        var answer = await Index().FindAsync("LCR", null, TestContext.Current.CancellationToken);

        Assert.Equal("needs_year", answer.Outcome);
        Assert.Equal(LcrShapes.LcrYears, answer.Years);
    }

    [Fact]
    public async Task NamesTheYearsThatDoExistWhenTheYearGivenDoesNot()
    {
        var answer = await Index().FindAsync("LCR", 2024, TestContext.Current.CancellationToken);

        Assert.Equal("needs_year", answer.Outcome);
        Assert.Equal(LcrShapes.LcrYears, answer.Years);
        Assert.Contains("2024", answer.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaysSoWhenNoSlugMatchesTheName()
    {
        var answer = await Index().FindAsync("NOPE", null, TestContext.Current.CancellationToken);

        Assert.Equal("unknown_product", answer.Outcome);
        Assert.Null(answer.ModelNo);
    }

    [Fact]
    public async Task SaysSoWhenTheYearExistsButNoCardStatesAModelNumber()
    {
        var answer = await Index().FindAsync("LCR", 2011, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
        Assert.Equal("lcr-2011", answer.Slug);
        Assert.Null(answer.ModelNo);
    }

    [Fact]
    public async Task NeverReturnsAModelNumberThePartsDatabaseDoesNotCarry()
    {
        // The card for lcr-2016 states 522199, which is not one of the six rows find_model returns.
        // A stale or mistyped card must not reach a parts lookup.
        var answer = await Index().FindAsync("LCR", 2016, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
        Assert.Null(answer.ModelNo);
    }

    [Fact]
    public async Task ResolvesWithoutAYearWhenTheProductHasOnlyOne()
    {
        var answer = await Index().FindAsync("SRVO", null, TestContext.Current.CancellationToken);

        Assert.Equal("model", answer.Outcome);
        Assert.Equal("srvo", answer.Slug);
    }

    [Fact]
    public async Task DoesNotLoseTheTurnWhenTheKnowledgeBaseRefuses()
    {
        // A knowledge base that throws is a model number nobody stated. The caller then asks for
        // the year, which is the answer it would have given anyway.
        ModelIndex index = new(LcrShapes.RefusingFacetRead(), LcrShapes.Vocabulary(), LcrShapes.Invoker());

        var answer = await index.FindAsync("LCR", 2023, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
    }

    [Fact]
    public async Task SaysSoWhenTheHostRegisteredNoKnowledgeBase()
    {
        ModelIndex index = new(cards: null, LcrShapes.Vocabulary(), LcrShapes.Invoker());

        var answer = await index.FindAsync("LCR", 2023, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
    }

    private static ModelIndex Index() =>
        new(LcrShapes.FacetRead(), LcrShapes.Vocabulary(), LcrShapes.Invoker());
}
