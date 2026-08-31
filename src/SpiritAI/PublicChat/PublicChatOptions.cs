namespace SpiritAI.PublicChat;

/// <summary>
/// The door the embeddable widget comes through, and what stops it being expensive.
/// </summary>
public sealed class PublicChatOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "PublicChat";

    /// <summary>
    /// The route the widget posts to.
    /// </summary>
    public string Pattern { get; set; } = "/v1/public/chat/completions";

    /// <summary>Whether the route is mapped at all. Turn it off and the widget stops working.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many requests one caller may make per <see cref="WindowSeconds"/>.</summary>
    public int PermitsPerWindow { get; set; } = 10;

    /// <summary>The length of the window, in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// How many public turns may run at once, across every caller.
    /// </summary>
    public int MaxConcurrentTurns { get; set; } = 20;
}
