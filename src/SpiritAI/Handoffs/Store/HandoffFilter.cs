namespace SpiritAI.Handoffs.Store;

/// <summary>
/// What one listing walks: which rows, whose, and from which end.
/// </summary>
/// <param name="View">Which rows.</param>
/// <param name="Order">Which end first.</param>
/// <param name="Owner">Whose rows. <see cref="HandoffOwner.Me"/> needs <paramref name="StaffKey"/>.</param>
/// <param name="StaffKey">The caller key <see cref="HandoffOwner.Me"/> means.</param>
public sealed record HandoffFilter(
    HandoffView View,
    HandoffOrder Order = HandoffOrder.OldestFirst,
    HandoffOwner Owner = HandoffOwner.Anyone,
    string? StaffKey = null)
{
    /// <summary>Refuses a filter that asks for the caller's rows without saying who the caller is.</summary>
    public void Check()
    {
        if (Owner == HandoffOwner.Me && string.IsNullOrEmpty(StaffKey))
        {
            throw new ArgumentException("A listing of the caller's own rows needs the caller's key.", nameof(StaffKey));
        }
    }
}
