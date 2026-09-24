using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>What <c>list_teams</c> tells the model.</summary>
/// <param name="Teams">Every team, with the description the model picks a team by.</param>
public sealed record ListTeamsAnswer(IReadOnlyList<ChatwootTeam> Teams);
