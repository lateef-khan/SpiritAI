using System.Diagnostics.CodeAnalysis;

using PhoneNumbers;

namespace SpiritAI.Handoffs.Model;

/// <summary>
/// Reads the phone number a visitor typed and shows it back to people. Numbers are stored in
/// E.164 form (<c>+12015550123</c>) and shown in international form (<c>+1 201-555-0123</c>).
/// A number without a country code is read as a US number.
/// </summary>
public static class VisitorPhone
{
    /// <summary>The region a number without a leading <c>+</c> is read in.</summary>
    public const string DefaultRegion = "US";

    private static readonly PhoneNumberUtil Numbers = PhoneNumberUtil.GetInstance();

    /// <summary>Reads a number as the visitor typed it.</summary>
    /// <param name="input">The text, in any common layout.</param>
    /// <param name="e164">The number in E.164 form, when it could be read.</param>
    /// <returns>Whether the text is a valid phone number.</returns>
    public static bool TryRead(string? input, [NotNullWhen(true)] out string? e164)
    {
        e164 = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        PhoneNumber number;

        try
        {
            number = Numbers.Parse(input, DefaultRegion);
        }
        catch (NumberParseException)
        {
            return false;
        }

        if (!Numbers.IsValidNumber(number))
        {
            return false;
        }

        e164 = Numbers.Format(number, PhoneNumberFormat.E164);
        return true;
    }

    /// <summary>The number as people read it.</summary>
    /// <param name="e164">A number <see cref="TryRead"/> gave back.</param>
    /// <returns>The international form, or the text unchanged when it cannot be read.</returns>
    public static string Display(string e164)
    {
        ArgumentNullException.ThrowIfNull(e164);

        try
        {
            return Numbers.Format(Numbers.Parse(e164, DefaultRegion), PhoneNumberFormat.INTERNATIONAL);
        }
        catch (NumberParseException)
        {
            return e164;
        }
    }
}
