using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// Reading a build year out of a model's name, and picking the model a year names.
/// </summary>
/// <remarks>
/// One product name covers several model numbers, and their parts lists are not the same list:
/// across the nine an F63 covers, one carries five lines and another carries two hundred and
/// nineteen. The year is what tells them apart, and the only place the year is written is the
/// model's own name, as "SOLE F63 2016". Everything here reads that name.
/// </remarks>
public sealed class ModelYearTests
{
    [Theory]
    [InlineData("SOLE F63 2013", 2013)]
    [InlineData("SOLE F63 2015", 2015)]
    [InlineData("Sole F63 2019", 2019)]
    [InlineData("XT385 2020", 2020)]
    public void ReadsTheYearOffTheName(string name, int expected)
        => Assert.Equal(expected, ModelYear.Read("563816", name)!.Year);

    [Theory]
    [InlineData("F63")]
    [InlineData("SOLE F63")]
    [InlineData("")]
    public void CarriesNoYearWhenTheNameNamesNone(string name)
        => Assert.Null(ModelYear.Read("563286", name)!.Year);

    [Fact]
    public void ReadsNoYearOutOfADigitRunThatIsNotOne()
    {
        // A part or model number in the name is not a year. Only 1990 to 2099 is read as one.
        Assert.Null(ModelYear.Read("563286", "F63 563286")!.Year);
    }

    [Fact]
    public void ReadsTheYearOutOfTheDescriptionWhenTheNameCarriesNone()
    {
        var model = ModelYear.Read("522112", "LCR", "FG, SOLE,  TREADMILL LCR 2013");

        Assert.Equal(2013, model.Year);
    }

    [Fact]
    public void TheNameWinsWhenBothCarryAYear()
    {
        var model = ModelYear.Read("522118", "Sole LCR 2019", "Bike Sole LCR 2013");

        Assert.Equal(2019, model.Year);
    }

    [Fact]
    public void KeepsTheNameAsTheNameEvenWhenTheYearCameFromTheDescription()
    {
        var model = ModelYear.Read("522112", "LCR", "FG, SOLE,  TREADMILL LCR 2013");

        Assert.Equal("LCR", model.Name);
    }

    [Fact]
    public void HasNoYearWhenNeitherCarriesOne()
    {
        var model = ModelYear.Read("522126", "LCR", "SOLE, LCR BIKE");

        Assert.Null(model.Year);
    }

    [Fact]
    public void ReadsTheNameAloneWhenNoDescriptionIsGiven()
    {
        var model = ModelYear.Read("563816", "SOLE F63 2016");

        Assert.Equal(2016, model.Year);
    }

    [Fact]
    public void PicksTheModelWhoseNameCarriesTheYear()
    {
        var picked = ModelYear.Pick(Nine(), 2016);

        Assert.Equal("563816", picked?.ModelNo);
    }

    [Fact]
    public void PicksNothingWhenNoModelCarriesThatYear()
        => Assert.Null(ModelYear.Pick(Nine(), 1999));

    [Fact]
    public void OffersOnlyTheYearsItActuallyHas()
    {
        var years = ModelYear.Years(Nine());

        Assert.Equal([2013, 2015, 2016, 2019], years);
    }

    /// <summary>The nine model numbers an F63 covers, named as the database names them.</summary>
    private static IReadOnlyList<ModelYear> Nine() =>
    [
        ModelYear.Read("563286", "F63"),
        ModelYear.Read("563812", "SOLE F63 2013"),
        ModelYear.Read("563814", "SOLE F63 2015"),
        ModelYear.Read("563816", "SOLE F63 2016"),
        ModelYear.Read("563818", "Sole F63 2019"),
        ModelYear.Read("563822", "F63"),
    ];
}
