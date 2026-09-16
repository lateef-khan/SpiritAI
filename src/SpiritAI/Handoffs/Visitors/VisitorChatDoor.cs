namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// The door in front of the public chat route, section 9.3 of the handoff spec. While a person has
/// the chat the bot must not answer, however stale the tab that asks it to.
/// </summary>
public static class VisitorChatDoor
{
    /// <summary>
    /// The <c>type</c> of the problem the door answers while a person has the chat. The widget
    /// matches on it and switches to the handoff messages route.
    /// </summary>
    public const string HandoffOpenType = "handoff_open";

    /// <summary>
    /// Lets a turn continue a chat the visitor owns while the bot has it, and refuses one while a
    /// person does.
    /// </summary>
    /// <param name="app">The application to add the door to. Add it after the token check.</param>
    /// <param name="pattern">The public chat route.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseVisitorChat(this IApplicationBuilder app, string pattern)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return app.UseMiddleware<VisitorChatMiddleware>(pattern);
    }
}
