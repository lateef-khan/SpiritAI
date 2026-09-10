using System.Text.Json;

using SpiritAI.Lookup;

using Xunit;

namespace SpiritAI.Tests.Lookup;

/// <summary>
/// Routing one desk question to the lookup that answers it.
/// </summary>
/// <remarks>
/// This replaces a second model call. The desk's job never varied — decide what it was handed and
/// read the matching record — so the deciding is done here, where it is testable and costs nothing.
/// </remarks>
public sealed class UnitDeskTests
{
    [Fact]
    public async Task ReadsTheMachineWhenGivenASerialNumber()
    {
        var answer = await Desk().ReadAsync(
            "5808881004036047", null, null, TestContext.Current.CancellationToken);

        Assert.NotNull(answer.Unit);
        Assert.Null(answer.Customer);
    }

    [Fact]
    public async Task ReadsTheWorkOrderWhenGivenAnOrderNumber()
    {
        var answer = await Desk().ReadAsync(
            null, "845435-1", null, TestContext.Current.CancellationToken);

        Assert.NotNull(answer.Order);
        Assert.Null(answer.Unit);
    }

    [Fact]
    public async Task ReadsTheCustomerWhenGivenAName()
    {
        var answer = await Desk().ReadAsync(
            null, null, "Jane Doe", TestContext.Current.CancellationToken);

        Assert.NotNull(answer.Customer);
        Assert.Equal("Jane Doe", Sent["Name"]);
    }

    [Fact]
    public async Task SearchesOnEmailWhenTheTextCarriesAnAtSign()
    {
        // find_units ANDs the three fields, measured against the live tool: sending one text as all
        // three matches nobody. So the shape of the text picks the one field to search on.
        await Desk().ReadAsync(null, null, "jane@example.com", TestContext.Current.CancellationToken);

        Assert.Equal("jane@example.com", Sent["Email"]);
        Assert.False(Sent.ContainsKey("Name"));
    }

    [Fact]
    public async Task SearchesOnPhoneWhenTheTextIsMostlyDigits()
    {
        await Desk().ReadAsync(null, null, "(801) 555-0100", TestContext.Current.CancellationToken);

        Assert.Equal("(801) 555-0100", Sent["Phone"]);
        Assert.False(Sent.ContainsKey("Name"));
    }

    [Fact]
    public async Task SearchesOnNameWhenTheTextIsNeither()
    {
        await Desk().ReadAsync(null, null, "Doe", TestContext.Current.CancellationToken);

        Assert.Equal("Doe", Sent["Name"]);
        Assert.False(Sent.ContainsKey("Phone"));
    }

    [Fact]
    public async Task SaysSoWhenNothingItWasGivenCanBeLookedUp()
    {
        var answer = await Desk().ReadAsync(null, null, null, TestContext.Current.CancellationToken);

        Assert.Null(answer.Unit);
        Assert.Null(answer.Order);
        Assert.Null(answer.Customer);
        Assert.NotEmpty(answer.Note);
    }

    [Fact]
    public async Task TreatsATooShortSerialAsACustomerRatherThanRefusing()
    {
        // A person types what they have. Sixteen digits is a serial and anything else is a name to
        // try, which is what the desk did and what keeps a mistyped number from ending the turn.
        var answer = await Desk().ReadAsync("58088", null, null, TestContext.Current.CancellationToken);

        Assert.Null(answer.Unit);
        Assert.NotNull(answer.Customer);
    }

    private static readonly Dictionary<string, object?> Sent = new(StringComparer.Ordinal);

    private static UnitDesk Desk()
    {
        Sent.Clear();

        ToolInvoker invoke = (toolId, arguments, _) =>
        {
            if (toolId == "find_units")
            {
                foreach (var (key, value) in arguments)
                {
                    Sent[key] = value;
                }
            }

            return ValueTask.FromResult(Json(toolId switch
            {
                "find_units" => UnitsJson,
                "get_work_orders" => OrdersJson,
                _ => RowsJson,
            }));
        };

        return new UnitDesk(new UnitLookup(invoke), new CustomerLookup(invoke));
    }

    private const string RowsJson = """
        {"status":"success","value":{"value":[{"SerialNo":"5808881004036047","ModelNo":"563816"}]}}
        """;

    private const string OrdersJson = """
        {"status":"success","value":{"value":[{"OrderNo":"845435-1","SerialNo":"5808881004036047"}]}}
        """;

    private const string UnitsJson = """
        {"status":"success","value":{"value":[
          {"SerialNo":"5808881004036047","ModelNo":"563816","ModelName":"SOLE F63 2016",
           "OpenOrders":1,"TotalOrders":4,"TotalRows":1}]}}
        """;

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();
}
