namespace SpiritAI.PublicChat;

/// <summary>The visitor's chat to open again.</summary>
/// <param name="RemoteId">The conversation id, as <c>ThreadCreated.RemoteId</c> names it.</param>
public sealed record LatestPublicThread(string RemoteId);
