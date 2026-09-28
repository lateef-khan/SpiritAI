namespace SpiritAI.Chatwoot;

/// <summary>
/// Where Chatwoot is and the keys Spirit talks to it with. <c>just chatwoot setup</c> prints every
/// value.
/// </summary>
public sealed class ChatwootOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Chatwoot";

    /// <summary>Chatwoot's address, such as <c>http://localhost:53000</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The Chatwoot account the Spirit inbox is in.</summary>
    public int AccountId { get; set; }

    /// <summary>The Spirit inbox's public identifier, which its public (contact) API is addressed by.</summary>
    public string InboxIdentifier { get; set; } = string.Empty;

    /// <summary>The agent bot's access token, which the AI's messages are posted with.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// The access token of a plain agent that Spirit reads the teams and the contact fields as, and
    /// saves a contact's phone and email with.
    /// </summary>
    public string ServiceToken { get; set; } = string.Empty;

    /// <summary>The Platform App's token: creates Desk users and asks for their one-time sign-in links.</summary>
    public string PlatformToken { get; set; } = string.Empty;

    /// <summary>
    /// An administrator's token.
    /// </summary>
    public string AdminToken { get; set; } = string.Empty;

    /// <summary>The Spirit inbox's numeric id, which inbox members are added to.</summary>
    public int InboxId { get; set; }

    /// <summary>Whether the AI can answer widget chats Chatwoot holds: every setting a turn needs is set.</summary>
    public bool TurnEnabled => BaseUrl.Length > 0 && AccountId > 0 && InboxIdentifier.Length > 0 && BotToken.Length > 0;
}
