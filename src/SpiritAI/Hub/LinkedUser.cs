namespace SpiritAI.Hub;

/// <summary>A Desk or CRM user recorded against a Person by its id in that app.</summary>
public sealed class LinkedUser
{
    /// <summary>The Person's id in <c>neon_auth."user"</c>, which is the token's <c>sub</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>One of <see cref="HubApps"/>.</summary>
    public required string App { get; set; }

    /// <summary>The user's id inside that app.</summary>
    public required string ExternalId { get; set; }

    /// <summary>
    /// <see langword="false"/> while a Desk create has made the user but not yet put it in the
    /// account and the inbox. Only ready links get a tile or a sign-in.
    /// </summary>
    public bool Ready { get; set; }
}
