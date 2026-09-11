using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// Turning a product name and a year into one model number.
/// </summary>
/// <remarks>
/// The number is read off <c>facets.model_number</c> by an exact facet read, never off a ranked
/// search and never off card prose. Ranking cannot separate 2023 from 2026 — measured, the 2026
/// card wins a search for the 2023 one — and the parts records name a year for some rows and not
/// others, so neither can decide this. The facet is then checked against the parts database before
/// the number is returned, because a stale card must not send a parts lookup to a machine nobody
/// built.
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
    public async Task SaysSoWhenTheYearExistsAndNobodyHasConfirmedAModelNumber()
    {
        var answer = await Index().FindAsync("LCR", 2011, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
        Assert.Equal("lcr-2011", answer.Slug);
        Assert.Null(answer.ModelNo);
    }

    [Fact]
    public async Task SaysSoWhenNoCardOfTheMachineCarriesTheFacet()
    {
        // The F63 is in the manuals and its cards carry no model number. That is the state of 36 of
        // the 197 machines, and it must read the same as a machine whose cards carry none: nothing
        // to say.
        var answer = await Index().FindAsync("F63", 2013, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
        Assert.Equal("f63-2013", answer.Slug);
        Assert.Null(answer.ModelNo);
    }

    [Fact]
    public async Task NeverReturnsAModelNumberThePartsDatabaseDoesNotCarry()
    {
        // The cards give lcr-2016 the number 522199, which is not one of the six rows find_model
        // returns. A stale card must not reach a parts lookup.
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
    public async Task SaysSoWhenTheCardsCarrySeveralModelNumbers()
    {
        // mt200-2022's cards carry 720080 and 720087, because the facet is a list and one machine
        // can hold several SKUs. Naming either would be a guess, so it reads as unconfirmed.
        var answer = await Index().FindAsync("MT200", 2022, TestContext.Current.CancellationToken);

        Assert.Equal("no_record", answer.Outcome);
        Assert.Equal("mt200-2022", answer.Slug);
        Assert.Null(answer.ModelNo);
    }

    private static ModelIndex Index() =>
        new(LcrShapes.Cards(), LcrShapes.Vocabulary(), LcrShapes.Invoker());
}
