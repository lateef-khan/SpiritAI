namespace SpiritAI.Lookup;

/// <summary>Small helpers the lookups would otherwise repeat.</summary>
internal static class LookupSpanExtensions
{
    /// <summary>Whether a span is one or more ASCII digits and nothing else.</summary>
    /// <param name="span">The span.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool ContainsOnlyDigits(this ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        foreach (var character in span)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
