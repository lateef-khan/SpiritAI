namespace SpiritAI.Chatwoot;

/// <summary>One team of the Chatwoot account.</summary>
/// <param name="Id">The team's id.</param>
/// <param name="Name">What staff call the team.</param>
/// <param name="Description">What the team handles, which the AI picks the team by.</param>
public sealed record ChatwootTeam(int Id, string Name, string Description);
