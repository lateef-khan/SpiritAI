namespace SpiritAI.Handoffs.Mail;

/// <summary>
/// The bridge to a visitor who left: a staff reply they are not there to read goes out as one
/// plain-text email. Section 8 of the handoff spec.
/// </summary>
/// <remarks>
/// A send that fails throws whatever the provider refused with. The words are already in the chat
/// and on the socket by the time this is called, so the caller decides what a miss costs, and the
/// desk decides it costs a log line.
/// </remarks>
public interface IHandoffMailer
{
    /// <summary>Sends one reply.</summary>
    /// <param name="mail">The reply and who it is for.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    ValueTask SendReplyAsync(HandoffReplyMail mail, CancellationToken cancellationToken);
}
