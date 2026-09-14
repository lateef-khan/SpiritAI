namespace SpiritAI.PublicChat;

/// <summary>
/// The door the embeddable widget comes through, and what stops it being expensive.
/// </summary>
public sealed class PublicChatOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "PublicChat";

    /// <summary>
    /// What every route a stranger may reach sits under: the chat, the public threads, and the
    /// visitor's handoff routes. The limiter counts all of them against one allowance, and the
    /// token check leaves all of them open.
    /// </summary>
    public string PublicPrefix { get; set; } = "/v1/public";

    /// <summary>
    /// The route the widget posts a turn to. Under <see cref="PublicPrefix"/>.
    /// </summary>
    public string Pattern { get; set; } = "/v1/public/responses";

    /// <summary>Whether the routes are mapped at all. Turn it off and the widget stops working.</summary>
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
