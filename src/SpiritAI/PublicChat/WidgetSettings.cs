namespace SpiritAI.PublicChat;

/// <summary>What the widget needs to talk to Chatwoot as the visitor. Nothing here is secret.</summary>
/// <param name="ChatwootBaseUrl">Chatwoot's address, which the Client API and the socket are under.</param>
/// <param name="ChatwootInboxIdentifier">The Spirit inbox's public identifier, which the Client API is addressed by.</param>
public sealed record WidgetSettings(string ChatwootBaseUrl, string ChatwootInboxIdentifier);
