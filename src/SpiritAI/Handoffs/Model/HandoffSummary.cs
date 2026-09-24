namespace SpiritAI.Handoffs.Model;

/// <summary>
/// What the AI tells staff about a chat when it asks for a person, so they can read it in the
/// seconds before they call. Each part is trimmed, cut to <see cref="MaxLength"/> characters, and
/// <see langword="null"/> when there is nothing in it.
/// </summary>
public sealed record HandoffSummary
{
    /// <summary>The most characters any one part keeps.</summary>
    public const int MaxLength = 500;

    /// <summary>A summary with nothing in it, for an ask the AI did not make.</summary>
    public static readonly HandoffSummary Empty = new(null, null, null, null);

    /// <summary>Cleans each part as it is given.</summary>
    /// <param name="product">The machine, type and model.</param>
    /// <param name="serial">The serial number, as text.</param>
    /// <param name="tried">What was already tried in the chat.</param>
    /// <param name="wants">What the person wants from staff.</param>
    public HandoffSummary(string? product, string? serial, string? tried, string? wants)
    {
        Product = Clean(product);
        Serial = Clean(serial);
        Tried = Clean(tried);
        Wants = Clean(wants);
    }

    /// <summary>The machine, type and model, such as "XT485 treadmill".</summary>
    public string? Product { get; }

    /// <summary>The serial number exactly as given, leading zeros kept.</summary>
    public string? Serial { get; }

    /// <summary>What was already tried in the chat.</summary>
    public string? Tried { get; }

    /// <summary>What the person wants from staff, such as "a technician visit".</summary>
    public string? Wants { get; }

    /// <summary>Whether every part is empty.</summary>
    public bool IsEmpty => Product is null && Serial is null && Tried is null && Wants is null;

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();

        return trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed;
    }
}
