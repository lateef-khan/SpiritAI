namespace SpiritAI.Handoffs.Store;

/// <summary>
/// Which rows a listing walks, and in what order.
/// </summary>
public enum HandoffView
{
    /// <summary>Every row a person still owes attention to: waiting and human, oldest ask first.</summary>
    Open,

    /// <summary>The queue alone, oldest ask first.</summary>
    Waiting,

    /// <summary>The closed rows, newest close first.</summary>
    Done,
}
