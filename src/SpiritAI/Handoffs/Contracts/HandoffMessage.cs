namespace SpiritAI.Handoffs.Contracts;

/// <summary>
/// One message of the human phase, as it is pushed and as a reply answers with it.
/// </summary>
/// <param name="CallId">The chat.</param>
/// <param name="Role">Whose side wrote it: <c>assistant</c> for staff and the host, <c>user</c> for the visitor.</param>
/// <param name="Text">The words.</param>
/// <param name="Speaker">Who wrote it, when that is not simply "the agent".</param>
/// <param name="At">When it was written.</param>
public sealed record HandoffMessage(
    string CallId,
    string Role,
    string Text,
    HandoffSpeaker? Speaker,
    DateTimeOffset At);
