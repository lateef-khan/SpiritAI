namespace SpiritAI.Chatwoot;

/// <summary>What Chatwoot did with a change to a contact.</summary>
/// <param name="Refusal">
/// Chatwoot's reason when it refused the change, such as "Phone number has already been taken";
/// <see langword="null"/> when it saved it.
/// </param>
public sealed record ChatwootContactUpdate(string? Refusal)
{
    /// <summary>A change Chatwoot saved.</summary>
    public static ChatwootContactUpdate Saved { get; } = new(Refusal: null);

    /// <summary>Whether Chatwoot saved the change.</summary>
    public bool WasSaved => Refusal is null;
}
