using Microsoft.Extensions.Options;

using Resend;

namespace SpiritAI.Handoffs.Mail;

/// <summary>
/// Sends a staff reply through Resend, as plain text with the staff mailbox as the reply address.
/// </summary>
/// <remarks>
/// Scoped and not singleton: the SDK's <see cref="IResend"/> is a typed <c>HttpClient</c> whose
/// options come as a scoped snapshot, and a singleton over it cannot be resolved once the host
/// validates scopes, which it does in Development.
/// </remarks>
public sealed class ResendHandoffMailer(IResend resend, IOptions<HandoffMailOptions> options) : IHandoffMailer
{
    /// <summary>The subject line every reply goes out under.</summary>
    public const string Subject = "Reply from Spirit support";

    /// <inheritdoc />
    public async ValueTask SendReplyAsync(HandoffReplyMail mail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var settings = options.Value;

        var message = new EmailMessage
        {
            From = settings.From,
            Subject = Subject,
            TextBody = BodyOf(mail),
        };

        message.To.Add(mail.To);

        if (!string.IsNullOrWhiteSpace(settings.ReplyTo))
        {
            message.ReplyTo = settings.ReplyTo;
        }

        var response = await resend.EmailSendAsync(message, cancellationToken).ConfigureAwait(false);

        // The SDK throws on a refusal by default. Should that ever be turned off, a refusal must
        // still be a throw here, because the desk's promise is "a failed send is logged".
        if (!response.Success)
        {
            throw (Exception?)response.Exception
                ?? new InvalidOperationException("Resend refused the email and gave no reason.");
        }
    }

    /// <summary>The text of the email: who replied, the words, and how to answer.</summary>
    /// <param name="mail">The reply.</param>
    /// <returns>Plain text, with bare line feeds; the provider does the rest.</returns>
    public static string BodyOf(HandoffReplyMail mail)
    {
        ArgumentNullException.ThrowIfNull(mail);

        return $"{mail.StaffName} from Spirit replied to your chat:\n\n{mail.Text}\n\nReply to this email to continue.";
    }
}
