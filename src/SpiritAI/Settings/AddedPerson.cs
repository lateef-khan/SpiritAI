namespace SpiritAI.Settings;

/// <summary>What an Add made.</summary>
/// <param name="Person">Their row, or <see langword="null"/> when the Neon step failed.</param>
/// <param name="Steps">Each step's outcome.</param>
public sealed record AddedPerson(PersonRow? Person, AddSteps Steps);
