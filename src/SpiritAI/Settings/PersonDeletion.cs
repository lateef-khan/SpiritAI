namespace SpiritAI.Settings;

/// <summary>What a Delete did, step by step. Pressing again does only what is left.</summary>
/// <param name="Neon">Run only once Desk and CRM are each none or done (ruling R14).</param>
/// <param name="Detail">Why a step failed or was held back; <see langword="null"/> when all went through.</param>
public sealed record PersonDeletion(StepResult Desk, StepResult Crm, StepResult Neon, string? Detail);
