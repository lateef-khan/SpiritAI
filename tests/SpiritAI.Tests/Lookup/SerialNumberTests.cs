using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// What a sixteen digit serial number says about itself, before any tool is called.
/// </summary>
/// <remarks>
/// The century window is the part worth pinning. A two digit year carries no century, so one has
/// to be chosen, and the choice is only wrong in one direction: a machine cannot be built after
/// today. Every test here passes the reference year rather than reading the clock, so the file
/// still says the same thing in 2100.
/// </remarks>
public sealed class SerialNumberTests
{
    /// <summary>The year the windowed tests below are read against.</summary>
    private const int Today = 2026;

    [Fact]
    public void Parse_Serial_ReadsTheModelNumberOffTheFront()
        => Assert.Equal("900112", SerialNumber.Parse("9001121004123456", Today).ModelNo);

    [Fact]
    public void Parse_Serial_ReadsTheMonthAndYear()
        => Assert.Equal("04/2010", SerialNumber.Parse("9001121004123456", Today).ManufacturedOn);

    [Fact]
    public void Parse_Serial_IsASerial()
        => Assert.True(SerialNumber.Parse("9001121004123456", Today).IsSerial);

    [Fact]
    public void Parse_YearOfThisYear_StaysInThisCentury()
        => Assert.Equal("01/2026", SerialNumber.Parse("9001122601123456", Today).ManufacturedOn);

    // A machine cannot be built after today, so a year that lands in the future belongs to the
    // century before. Sole has been selling since the late nineties and those serials still exist.
    [Fact]
    public void Parse_YearInTheFuture_RollsBackACentury()
        => Assert.Equal("04/1999", SerialNumber.Parse("9001129904123456", Today).ManufacturedOn);

    [Theory]
    [InlineData("9001121013123456")]
    [InlineData("9001121000123456")]
    public void Parse_MonthOutOfRange_KeepsTheModelAndDropsTheDate(string serial)
    {
        var facts = SerialNumber.Parse(serial, Today);

        Assert.Equal("900112", facts.ModelNo);
        Assert.Null(facts.ManufacturedOn);
    }

    [Theory]
    // Fifteen digits.
    [InlineData("900112100412345")]
    // Seventeen digits.
    [InlineData("90011210041234567")]
    // Sixteen characters, but not sixteen digits.
    [InlineData("90011210041234AB")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_NotSixteenDigits_ReadsNothing(string? serial)
    {
        var facts = SerialNumber.Parse(serial, Today);

        Assert.False(facts.IsSerial);
        Assert.Null(facts.ModelNo);
        Assert.Null(facts.ManufacturedOn);
    }

    // The agent is told to say how many digits it counted when a number is not a serial, so the
    // count is part of the answer rather than something the model works out from the string.
    [Theory]
    [InlineData("9001121004123456", 16)]
    [InlineData("900112-1004-1234", 14)]
    [InlineData("no digits here", 0)]
    [InlineData(null, 0)]
    public void Parse_CountsTheDigitsItWasGiven(string? serial, int expected)
        => Assert.Equal(expected, SerialNumber.Parse(serial, Today).Digits);
}
