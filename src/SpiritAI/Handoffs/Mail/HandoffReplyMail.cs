namespace SpiritAI.Handoffs.Mail;

/// <summary>
/// One staff reply, ready to be mailed.
/// </summary>
/// <param name="To">The visitor's address, as they left it on the handoff.</param>
/// <param name="ConversationId">The chat the reply is on. Not shown to the visitor; named in the log when the send fails.</param>
/// <param name="StaffName">Who replied, as the visitor sees the name above the message.</param>
/// <param name="Text">The words.</param>
public sealed record HandoffReplyMail(string To, string ConversationId, string StaffName, string Text);
