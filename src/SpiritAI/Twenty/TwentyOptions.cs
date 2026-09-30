namespace SpiritAI.Twenty;

/// <summary>How Spirit reaches the Twenty fork's own endpoints.</summary>
public sealed class TwentyOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Twenty";

    /// <summary>Twenty's address as Spirit's server reaches it.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The secret the fork checks notes and user creates with. Same value as <c>TWENTY_SPIRIT_HUB_SECRET</c>.</summary>
    public string HubSecret { get; set; } = string.Empty;
}
