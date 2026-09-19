using System.Text.Json.Serialization;

namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// What the widget posts when the visitor taps <b>Talk to a person</b>.
/// </summary>
/// <param name="ConversationId">The chat, the one <c>POST /v1/public/threads</c> answered with.</param>
/// <param name="Reason">What the person is for, when the visitor said.</param>
public sealed record VisitorAskRequest([property: JsonPropertyName("callId")] string ConversationId, string? Reason);
