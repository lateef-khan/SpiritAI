namespace SpiritAI.Chatwoot;

/// <summary>A visitor started or stopped typing in a chat.</summary>
/// <param name="ConversationId">The chat.</param>
/// <param name="On"><see langword="true"/> for typing, <see langword="false"/> for stopped.</param>
public sealed record VisitorTyping(string ConversationId, bool On);
