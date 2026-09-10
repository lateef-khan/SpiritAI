namespace SpiritAI.Lookup;

/// <summary>
/// Whichever record a desk question found.
/// </summary>
/// <remarks>
/// Exactly one of the three is set, or none of them and <see cref="Note"/> says why. The desk this
/// replaces answered in prose; a caller reading this one cannot mistake a machine for a customer.
/// </remarks>
/// <param name="Unit">The machine, when a serial number found one.</param>
/// <param name="Order">The work order, when an order number found one.</param>
/// <param name="Customer">The machines a customer owns, when a name, email or phone found any.</param>
/// <param name="Note">One line the agent may repeat about what happened.</param>
public sealed record UnitDeskAnswer(
    UnitDocument? Unit,
    OrderDocument? Order,
    CustomerUnits? Customer,
    string Note);
