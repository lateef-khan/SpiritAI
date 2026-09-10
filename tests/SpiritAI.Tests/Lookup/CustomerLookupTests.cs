using System.Text.Json;

using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// The shaping between <c>find_units</c>' rowset and the machines one customer owns.
/// </summary>
/// <remarks>
/// The columns below are the ones the live tool answers with, read off it rather than assumed:
/// SerialNo, ModelNo, ModelName, PurchasedDate, SetupDate, OpenOrders, TotalOrders and TotalRows.
/// The tool returns no contact details by design, and neither does this.
/// </remarks>
public sealed class CustomerLookupTests
{
    [Fact]
    public async Task FindsTheMachinesOneCustomerOwns()
    {
        var units = await Lookup().FindCustomerUnitsAsync(
            "Jane Doe", null, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, units.Units.Count);
        Assert.Contains(units.Units, unit => unit.SerialNo == "5808881004036047");
    }

    [Fact]
    public async Task ReadsEveryColumnTheToolAnswersWith()
    {
        var units = await Lookup().FindCustomerUnitsAsync(
            "Jane Doe", null, null, TestContext.Current.CancellationToken);

        var unit = units.Units[0];

        Assert.Equal("563816", unit.ModelNo);
        Assert.Equal("SOLE F63 2016", unit.ModelName);
        Assert.Equal(2016, unit.PurchasedOn?.Year);
        Assert.Equal(2016, unit.SetUpOn?.Year);
        Assert.Equal(1, unit.OpenOrders);
        Assert.Equal(4, unit.TotalOrders);
    }

    [Fact]
    public async Task ReportsTheTrueTotalAndNotTheRowsItWasSent()
    {
        // TotalRows is the count before Top, so a customer with more machines than one page still
        // hears how many they own.
        var units = await Lookup().FindCustomerUnitsAsync(
            "Jane Doe", null, null, TestContext.Current.CancellationToken);

        Assert.Equal(7, units.TotalRows);
    }

    [Fact]
    public async Task RefusesWhenEveryFieldIsTooShortToSearch()
    {
        var units = await Lookup().FindCustomerUnitsAsync(
            "Jo", null, null, TestContext.Current.CancellationToken);

        Assert.Empty(units.Units);
        Assert.Contains("three", units.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefusesWhenNothingIdentifiesTheCustomer()
    {
        var units = await Lookup().FindCustomerUnitsAsync(
            null, null, null, TestContext.Current.CancellationToken);

        Assert.Empty(units.Units);
    }

    [Fact]
    public async Task SearchesOnAPhoneOfThreeCharactersOrMore()
    {
        IReadOnlyDictionary<string, object?>? sent = null;

        var lookup = new CustomerLookup((_, arguments, _) =>
        {
            sent = arguments;
            return ValueTask.FromResult(Json(UnitsJson));
        });

        await lookup.FindCustomerUnitsAsync(
            null, null, "(801) 555-0100", TestContext.Current.CancellationToken);

        Assert.Equal("(801) 555-0100", sent!["Phone"]);
        Assert.False(sent.ContainsKey("Name"));
    }

    [Fact]
    public async Task SaysSoWhenNoCustomerMatches()
    {
        var units = await Lookup(Empty).FindCustomerUnitsAsync(
            "Nobody At All", null, null, TestContext.Current.CancellationToken);

        Assert.Empty(units.Units);
        Assert.Contains("no", units.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DoesNotLoseTheTurnWhenTheToolThrows()
    {
        var lookup = new CustomerLookup((_, _, _)
            => throw new InvalidOperationException("the database is unreachable."));

        var units = await lookup.FindCustomerUnitsAsync(
            "Jane Doe", null, null, TestContext.Current.CancellationToken);

        Assert.Empty(units.Units);
    }

    private const string UnitsJson = """
        {"status":"success","value":{"value":[
          {"SerialNo":"5808881004036047","ModelNo":"563816","ModelName":"SOLE F63 2016",
           "PurchasedDate":"2016-04-11T00:00:00","SetupDate":"2016-04-20T00:00:00",
           "OpenOrders":1,"TotalOrders":4,"TotalRows":7},
          {"SerialNo":"5222221004036048","ModelNo":"522122","ModelName":"LCR",
           "PurchasedDate":"2023-09-02T00:00:00","SetupDate":null,
           "OpenOrders":0,"TotalOrders":1,"TotalRows":7}
        ]}}
        """;

    private const string Empty = """{"status":"success","value":{"value":[]}}""";

    private static CustomerLookup Lookup(string? units = null)
        => new((_, _, _) => ValueTask.FromResult(Json(units ?? UnitsJson)));

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
}
