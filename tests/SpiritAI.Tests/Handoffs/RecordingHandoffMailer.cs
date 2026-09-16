using SpiritAI.Handoffs.Mail;

namespace SpiritAI.Tests.Handoffs;

/// <summary>An <see cref="IHandoffMailer"/> that keeps every reply it was asked to send.</summary>
internal sealed class RecordingHandoffMailer : IHandoffMailer
{
    /// <summary>Every send, in order.</summary>
    public List<HandoffReplyMail> Sent { get; } = [];

    public ValueTask SendReplyAsync(HandoffReplyMail mail, CancellationToken cancellationToken)
    {
        Sent.Add(mail);
        return ValueTask.CompletedTask;
    }
}
