namespace SpiritAI.Chatwoot;

/// <summary>One Chatwoot agent and the status Chatwoot shows for them.</summary>
/// <param name="Id">The agent's user id.</param>
/// <param name="AvailabilityStatus"><c>online</c>, <c>busy</c>, or <c>offline</c>.</param>
public sealed record ChatwootAgent(int Id, string AvailabilityStatus)
{
    /// <summary>Whether Chatwoot shows the agent as online.</summary>
    public bool Online => AvailabilityStatus == "online";
}
