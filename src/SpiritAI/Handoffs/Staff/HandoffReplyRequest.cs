namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// What a member of staff posts to a chat they hold.
/// </summary>
/// <param name="Text">The words. Blank is refused.</param>
public sealed record HandoffReplyRequest(string Text);
