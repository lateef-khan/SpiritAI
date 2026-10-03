namespace SpiritAI.Settings;

/// <summary>Each step of an Add. A failed Neon step made nothing; a later failed step is finished from the person's row.</summary>
/// <param name="Detail">Why a step failed, for the admin to read; <see langword="null"/> when none did.</param>
public sealed record AddSteps(StepResult Neon, StepResult Roles, StepResult Desk, StepResult Crm, string? Detail);
