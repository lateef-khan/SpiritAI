namespace SpiritAI.Lookup;

/// <summary>
/// One desk question, routed to the lookup that answers it.
/// </summary>
/// <remarks>
/// This replaces an agent. The unit desk's job never varied — decide what it was handed, then read
/// the matching record — so the deciding happens here, where it is testable, costs no model call,
/// and cannot invent a serial number it did not receive.
/// </remarks>
/// <param name="units">Reads one machine or one work order.</param>
/// <param name="customers">Reads the machines one customer owns.</param>
public sealed class UnitDesk(UnitLookup units, CustomerLookup customers)
{
    /// <summary>The fewest digits a string needs before it is read as a phone number.</summary>
    private const int ShortestPhone = 7;

    private readonly UnitLookup _units = units;
    private readonly CustomerLookup _customers = customers;

    /// <summary>Reads whichever record the caller gave enough to find.</summary>
    /// <param name="serialNo">A sixteen digit serial number, or nothing.</param>
    /// <param name="orderNumber">A work order number such as <c>845435-1</c>, or nothing.</param>
    /// <param name="customer">A customer's name, email or phone, or nothing.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The one record that answered, or a note saying nothing could be looked up.</returns>
    public async Task<UnitDeskAnswer> ReadAsync(
        string? serialNo,
        string? orderNumber,
        string? customer,
        CancellationToken cancellationToken = default)
    {
        if (UnitLookup.IsSerial(serialNo))
        {
            var unit = await _units.ReadUnitAsync(serialNo!, cancellationToken).ConfigureAwait(false);

            return new UnitDeskAnswer(unit, null, null, unit is null ? $"No machine is on record for {serialNo}." : string.Empty);
        }

        if (UnitLookup.IsOrderNumber(orderNumber))
        {
            var order = await _units.ReadOrderAsync(orderNumber!, cancellationToken).ConfigureAwait(false);

            return new UnitDeskAnswer(null, order, null, order is null ? $"No work order {orderNumber} is on record." : string.Empty);
        }

        // A person types what they have. Sixteen digits is a serial and anything else is a name to
        // try, so a mistyped number becomes a search rather than the end of the turn.
        if ((Searchable(customer) ?? Searchable(serialNo) ?? Searchable(orderNumber)) is { } text)
        {
            var found = await SearchAsync(text, cancellationToken).ConfigureAwait(false);

            return new UnitDeskAnswer(null, null, found, found.Note);
        }

        return new UnitDeskAnswer(
            null,
            null,
            null,
            "Nothing here identifies a machine. A serial number, a work order number, or a "
            + "customer's name, email or phone will find one.");
    }

    /// <summary>
    /// Searches on the one customer field the text looks like.
    /// </summary>
    /// <remarks>
    /// <c>find_units</c> ANDs its three fields — measured against the live tool, sending one text
    /// as all three matches nobody. So the shape of the text picks exactly one field.
    /// </remarks>
    private Task<CustomerUnits> SearchAsync(string text, CancellationToken cancellationToken)
    {
        if (text.Contains('@', StringComparison.Ordinal))
        {
            return _customers.FindCustomerUnitsAsync(null, text, null, cancellationToken);
        }

        return text.Count(char.IsAsciiDigit) >= ShortestPhone
            ? _customers.FindCustomerUnitsAsync(null, null, text, cancellationToken)
            : _customers.FindCustomerUnitsAsync(text, null, null, cancellationToken);
    }

    /// <summary>The text, when there is any of it.</summary>
    private static string? Searchable(string? text)
        => text?.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
