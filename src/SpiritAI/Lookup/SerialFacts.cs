namespace SpiritAI.Lookup;

/// <summary>
/// What a serial number says about itself, read out of the number alone.
/// </summary>
/// <remarks>
/// No database is behind this. It is what the digits mean, which is why it still answers for a
/// machine nobody has sold and why it costs no round trip.
/// </remarks>
/// <param name="IsSerial">Whether the text is a serial number: sixteen digits and nothing else.</param>
/// <param name="Digits">
/// How many digits were in the text. Reported so a refusal can say what was counted rather than
/// leave the count to be worked out from the string.
/// </param>
/// <param name="ModelNo">The six digits the serial starts with, or <see langword="null"/> when it is not a serial.</param>
/// <param name="ManufacturedOn">
/// The month and year the digits name, as <c>04/2010</c>. Not a date: the serial carries no day,
/// and <see cref="UnitHeader.ManufacturedOn" /> holds the same fact in the same shape. It is
/// <see langword="null"/> when the two month digits name no month.
/// </param>
public sealed record SerialFacts(bool IsSerial, int Digits, string? ModelNo, string? ManufacturedOn);
