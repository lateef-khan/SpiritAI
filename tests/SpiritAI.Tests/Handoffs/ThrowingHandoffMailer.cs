using SpiritAI.Handoffs.Mail;

namespace SpiritAI.Tests.Handoffs;

/// <summary>An <see cref="IHandoffMailer"/> whose provider is down: every send fails.</summary>
internal sealed class ThrowingHandoffMailer : IHandoffMailer
{
    public ValueTask SendReplyAsync(HandoffReplyMail mail, CancellationToken cancellationToken)
        => throw new HttpRequestException("Resend answered 500.");
}
