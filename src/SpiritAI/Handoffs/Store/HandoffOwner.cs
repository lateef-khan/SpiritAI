namespace SpiritAI.Handoffs.Store;

/// <summary>
/// Which rows a listing keeps, by who holds them.
/// </summary>
public enum HandoffOwner
{
    /// <summary>Every row, held or not.</summary>
    Anyone,

    /// <summary>The rows the caller holds.</summary>
    Me,

    /// <summary>The rows nobody has taken.</summary>
    Nobody,
}
