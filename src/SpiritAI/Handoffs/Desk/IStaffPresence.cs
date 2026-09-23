namespace SpiritAI.Handoffs.Desk;

/// <summary>How many members of staff are online to take a chat.</summary>
public interface IStaffPresence
{
    /// <summary>Counts the staff online now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Distinct people. Zero when the count cannot be read.</returns>
    Task<int> CountOnlineAsync(CancellationToken cancellationToken);
}
