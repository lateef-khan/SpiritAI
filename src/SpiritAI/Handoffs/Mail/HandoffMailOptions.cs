namespace SpiritAI.Handoffs.Mail;

/// <summary>
/// How a staff reply reaches a visitor who left, bound from the <see cref="SectionName"/> section.
/// </summary>
public sealed class HandoffMailOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Handoff:Mail";

    /// <summary>
    /// Whether replies are mailed at all. Off, nothing is sent and nothing below is needed: the
    /// reply still lands in the chat and on the socket. On, <see cref="From"/> and
    /// <see cref="ApiKey"/> must be set, and the host refuses to start until they are.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The sender, as the visitor's mail client shows it: <c>Spirit Support &lt;support@spiritfitness.com&gt;</c>.
    /// The domain must be verified in Resend, or every send is refused.
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// The mailbox a visitor's reply lands in: a staff mailbox, so "reply to this email" reaches a
    /// person. Empty means replies go back to <see cref="From"/>.
    /// </summary>
    public string ReplyTo { get; set; } = string.Empty;

    /// <summary>
    /// The Resend API key. A secret: on Fly it is set with <c>fly secrets set</c>, never in
    /// <c>fly.toml</c> or a settings file.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Whether the host can start with these.</summary>
    /// <param name="problem">What is missing, naming the setting, or empty when nothing is.</param>
    /// <returns><see langword="true"/> when mail is off, or on with everything it needs.</returns>
    public bool IsUsable(out string problem)
    {
        if (Enabled && string.IsNullOrWhiteSpace(From))
        {
            problem = $"{SectionName}:From is not set. Mail is on, so put the sender there, "
                + "such as \"Spirit Support <support@spiritfitness.com>\".";
            return false;
        }

        if (Enabled && string.IsNullOrWhiteSpace(ApiKey))
        {
            problem = $"{SectionName}:ApiKey is not set. Mail is on, so put the Resend API key there: "
                + "on Fly, `fly secrets set Handoff__Mail__ApiKey=<key>`.";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}
