namespace SpiritAI.Chatwoot;

/// <summary>One conversation of a contact, as the contact's list shows it.</summary>
/// <param name="Id">The display id, which is also the visitor's code.</param>
/// <param name="Status"><c>pending</c>, <c>open</c>, <c>resolved</c>, or <c>snoozed</c>.</param>
/// <param name="LastActivityAt">When anything last happened in it.</param>
/// <param name="Team">The team it is given to, or null.</param>
public sealed record ChatwootContactConversation(int Id, string Status, DateTimeOffset LastActivityAt, ChatwootTeam? Team)
{
    /// <summary>Whether staff have it: the handoff opened it and nobody has resolved it.</summary>
    public bool Open => Status == "open";
}
