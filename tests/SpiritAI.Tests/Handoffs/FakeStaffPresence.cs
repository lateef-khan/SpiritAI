using SpiritAI.Handoffs.Desk;

namespace SpiritAI.Tests.Handoffs;

/// <summary>An <see cref="IStaffPresence"/> that says whatever count the test sets.</summary>
internal sealed class FakeStaffPresence : IStaffPresence
{
    /// <summary>How many staff are online.</summary>
    public int Online { get; set; }

    /// <summary>How many times the count was read.</summary>
    public int Reads { get; private set; }

    public Task<int> CountOnlineAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Online);
    }
}
