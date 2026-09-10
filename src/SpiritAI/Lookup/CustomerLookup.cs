using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Finds the machines one customer owns, straight from the tool the agent uses.
/// </summary>
/// <param name="invoke">The seam that calls one DAB tool.</param>
public sealed class CustomerLookup(ToolInvoker invoke)
{
    /// <summary>The tool id <c>spirit.yaml</c> aliases the customer reader under.</summary>
    private const string FindUnits = "find_units";

    /// <summary>The fewest characters the records will search a customer field on.</summary>
    private const int ShortestSearch = 3;

    /// <summary>The most machines one answer carries.</summary>
    private const int Cap = 20;

    private readonly ToolInvoker _invoke = invoke;

    /// <summary>Finds the machines one customer owns.</summary>
    /// <param name="name">The customer's name, or nothing.</param>
    /// <param name="email">The customer's email, or nothing.</param>
    /// <param name="phone">The customer's phone, in any format, or nothing.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The machines, or an empty list and a note saying why.</returns>
    public async Task<CustomerUnits> FindCustomerUnitsAsync(
        string? name,
        string? email,
        string? phone,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, object?> arguments = new(StringComparer.Ordinal) { ["Top"] = Cap };

        Add(arguments, "Name", name);
        Add(arguments, "Email", email);
        Add(arguments, "Phone", phone);

        // Only Top is set, so nothing was given that the records will search on.
        if (arguments.Count == 1)
        {
            return new CustomerUnits(
                [],
                0,
                "A customer search needs a name, an email or a phone number, of three characters or more.");
        }

        var rows = await ReadAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (rows is null || rows.Count == 0)
        {
            return new CustomerUnits([], 0, "No customer matches that, so no machine is on record for them.");
        }

        var units = rows.Select(UnitOf).ToArray();

        // TotalRows is the count before Top, so a customer with more machines than one page still
        // hears how many they own.
        var total = DabRow.Number(rows[0], "TotalRows") ?? units.Length;

        return new CustomerUnits(units, total, $"{units.Length} of {total} machines.");
    }

    /// <summary>Adds one customer field, when it is long enough for the records to search on.</summary>
    private static void Add(Dictionary<string, object?> arguments, string field, string? value)
    {
        if (value?.Trim() is { Length: >= ShortestSearch } searchable)
        {
            arguments[field] = searchable;
        }
    }

    /// <summary>Reads one machine out of a <c>find_units</c> row.</summary>
    private static CustomerUnit UnitOf(JsonElement row) => new(
        DabRow.Text(row, "SerialNo") ?? string.Empty,
        DabRow.Text(row, "ModelNo"),
        DabRow.Text(row, "ModelName"),
        DabRow.Moment(row, "PurchasedDate"),
        DabRow.Moment(row, "SetupDate"),
        DabRow.Number(row, "OpenOrders"),
        DabRow.Number(row, "TotalOrders"));

    private async Task<IReadOnlyList<JsonElement>?> ReadAsync(
        Dictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        try
        {
            return DabEnvelope.RowsOf(
                await _invoke(FindUnits, arguments, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tool that throws is a question this cannot answer, never a request that fails. The
            // caller says so in one line and the turn carries on.
            return null;
        }
    }
}
