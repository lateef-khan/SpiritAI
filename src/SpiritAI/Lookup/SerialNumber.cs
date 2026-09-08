using System.Globalization;

namespace SpiritAI.Lookup;

/// <summary>
/// Reads a Spirit serial number without asking anything.
/// </summary>
public static class SerialNumber
{
    /// <summary>How many characters of a serial name the model.</summary>
    private const int ModelDigits = 6;

    /// <summary>Where the two year digits start.</summary>
    private const int YearAt = 6;

    /// <summary>Where the two month digits start.</summary>
    private const int MonthAt = 8;

    /// <summary>Reads a serial number against the current year.</summary>
    /// <param name="serial">Whatever the caller sent. It need not be a serial number.</param>
    /// <returns>What the digits mean, or a refusal that says how many digits there were.</returns>
    public static SerialFacts Parse(string? serial)
        => Parse(serial, DateTime.UtcNow.Year);

    /// <summary>Reads a serial number against a given year.</summary>
    /// <remarks>
    /// The year is a parameter so the century window below can be tested without the clock. Nothing
    /// but a test passes anything other than today.
    /// </remarks>
    /// <param name="serial">Whatever the caller sent. It need not be a serial number.</param>
    /// <param name="currentYear">The year to read the two digit year against.</param>
    /// <returns>What the digits mean, or a refusal that says how many digits there were.</returns>
    public static SerialFacts Parse(string? serial, int currentYear)
    {
        if (serial is null)
        {
            return new SerialFacts(false, 0, null, null);
        }

        var digits = serial.Count(char.IsAsciiDigit);

        if (!UnitLookup.IsSerial(serial))
        {
            return new SerialFacts(false, digits, null, null);
        }

        var month = int.Parse(serial.AsSpan(MonthAt, 2), CultureInfo.InvariantCulture);

        if (month is < 1 or > 12)
        {
            return new SerialFacts(true, digits, serial[..ModelDigits], null);
        }

        var year = int.Parse(serial.AsSpan(YearAt, 2), CultureInfo.InvariantCulture);
        var full = ((currentYear / 100) * 100) + year;

        // Two digits carry no century, so one has to be chosen, and the choice is only ever wrong
        // in one direction: a machine cannot have been built after today. A '99' read in 2026 is
        // 1999, and those serials are still in the field.
        if (full > currentYear)
        {
            full -= 100;
        }

        return new SerialFacts(
            true,
            digits,
            serial[..ModelDigits],
            string.Create(CultureInfo.InvariantCulture, $"{month:00}/{full:0000}"));
    }
}
