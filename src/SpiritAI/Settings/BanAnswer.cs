namespace SpiritAI.Settings;

/// <summary>What a Ban or an Unban did. The ban itself is saved before the Desk step runs.</summary>
/// <param name="Person">Their row after the change.</param>
/// <param name="Desk">Leaving the Chatwoot account on a ban, joining it again on an unban; none without a Desk link.</param>
/// <param name="Detail">Why the Desk step failed; <see langword="null"/> when it did not.</param>
public sealed record BanAnswer(PersonRow Person, StepResult Desk, string? Detail);
