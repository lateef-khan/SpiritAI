namespace SpiritAI.Handoffs;

/// <summary>
/// The answer to a claim: how it went, and the row as it stands afterwards.
/// </summary>
/// <param name="Result">How the claim went.</param>
/// <param name="Row">
/// The open row after the claim, naming whoever holds it. <see langword="null"/> only for
/// <see cref="HandoffClaimResult.NotWaiting"/>, when there is no open row to show.
/// </param>
public sealed record HandoffClaim(HandoffClaimResult Result, Handoff? Row)
{
    /// <summary>The claim took the chat.</summary>
    /// <param name="row">The row, now naming the claimant.</param>
    public static HandoffClaim Won(Handoff row) => new(HandoffClaimResult.Won, row);

    /// <summary>Somebody else already holds the chat.</summary>
    /// <param name="row">The row, naming them.</param>
    public static HandoffClaim AlreadyTaken(Handoff row) => new(HandoffClaimResult.AlreadyTaken, row);

    /// <summary>There is nothing open to claim.</summary>
    public static HandoffClaim NotWaiting() => new(HandoffClaimResult.NotWaiting, null);
}
