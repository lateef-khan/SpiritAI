using System.Net.Mail;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// Checks the phone and email a person gave for a call back. Checked means well-formed, not that
/// the number or address is theirs.
/// </summary>
public static class CheckContactTool
{
    /// <summary>The name <see cref="CheckContactAnswer.Invalid"/> gives a phone that is not valid.</summary>
    public const string PhoneField = "phone";

    /// <summary>The name <see cref="CheckContactAnswer.Invalid"/> gives an email that is not valid.</summary>
    public const string EmailField = "email";

    /// <summary>Checks what the person gave.</summary>
    /// <param name="phone">The phone as they typed it, or nothing.</param>
    /// <param name="email">The email as they typed it, or nothing.</param>
    /// <returns>The checked values, and which given field is not valid.</returns>
    public static CheckContactAnswer Check(string? phone, string? email)
    {
        List<string> invalid = [];

        if (!VisitorPhone.TryRead(phone, out var e164) && !string.IsNullOrWhiteSpace(phone))
        {
            invalid.Add(PhoneField);
        }

        var address = ReadEmail(email);

        if (address is null && !string.IsNullOrWhiteSpace(email))
        {
            invalid.Add(EmailField);
        }

        return new CheckContactAnswer(e164, address, invalid);
    }

    /// <summary>
    /// An address alone, with no display name, and a dot in its domain: <see cref="MailAddress"/>
    /// accepts <c>dana@localhost</c> and <c>Dana &lt;dana@example.com&gt;</c>, which no call back needs.
    /// </summary>
    private static string? ReadEmail(string? email)
    {
        var trimmed = email?.Trim();

        return MailAddress.TryCreate(trimmed, out var address)
            && string.Equals(address.Address, trimmed, StringComparison.Ordinal)
            && address.Host.Contains('.', StringComparison.Ordinal)
            && !address.Host.EndsWith('.')
                ? address.Address.ToLowerInvariant()
                : null;
    }
}
