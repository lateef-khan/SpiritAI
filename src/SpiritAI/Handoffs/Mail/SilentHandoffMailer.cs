namespace SpiritAI.Handoffs.Mail;

/// <summary>
/// The mailer a host has while <see cref="HandoffMailOptions.Enabled"/> is off: every reply is
/// dropped with a line in the debug log, so a quiet inbox can be told from a broken one.
/// </summary>
internal sealed class SilentHandoffMailer(ILogger<SilentHandoffMailer> logger) : IHandoffMailer
{
    /// <inheritdoc />
    public ValueTask SendReplyAsync(HandoffReplyMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);

        logger.LogDebug("Mail is off. The reply on call {CallId} was not mailed.", mail.CallId);

        return ValueTask.CompletedTask;
    }
}
