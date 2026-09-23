namespace SpiritAI.Chatwoot;

/// <summary>
/// Where Chatwoot is and the keys Spirit talks to it with. <c>just chatwoot setup</c> prints every
/// value but <see cref="WebhookPattern"/> and <see cref="MaxClockSkewSeconds"/>.
/// </summary>
public sealed class ChatwootOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Chatwoot";

    /// <summary>Chatwoot's address, such as <c>http://localhost:53000</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The Chatwoot account the Spirit inbox is in.</summary>
    public int AccountId { get; set; }

    /// <summary>The Spirit API inbox.</summary>
    public int InboxId { get; set; }

    /// <summary>The Spirit inbox's public identifier, which its public (contact) API is addressed by.</summary>
    public string InboxIdentifier { get; set; } = string.Empty;

    /// <summary>The inbox secret Chatwoot signs its webhooks with. Empty turns the webhook off.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>The agent bot's access token, which the AI's messages are posted with.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// The access token of a plain agent that Spirit reads who is online as.
    /// </summary>
    public string ServiceToken { get; set; } = string.Empty;

    /// <summary>The route Chatwoot's inbox webhook posts to.</summary>
    public string WebhookPattern { get; set; } = "/chatwoot/webhook";

    /// <summary>How far a webhook's timestamp may be from now before it is refused as a replay.</summary>
    public int MaxClockSkewSeconds { get; set; } = 300;

    /// <summary>Whether the webhook is on.</summary>
    public bool WebhookEnabled => WebhookSecret.Length > 0;

    /// <summary>Whether who is online is read from Chatwoot: every setting the read needs is set.</summary>
    public bool PresenceEnabled => BaseUrl.Length > 0 && AccountId > 0 && ServiceToken.Length > 0;

    /// <summary>Whether widget chats are copied into Chatwoot: every setting the copy needs is set.</summary>
    public bool CopyEnabled => BaseUrl.Length > 0 && AccountId > 0 && InboxId > 0 && InboxIdentifier.Length > 0 && BotToken.Length > 0;
}
