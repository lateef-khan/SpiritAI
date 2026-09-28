namespace SpiritAI.Chatwoot;

/// <summary>Puts <see cref="ChatwootTurnMiddleware"/> in the pipeline.</summary>
public static class ChatwootTurnDoor
{
    /// <summary>
    /// Lets a public turn run only on the visitor's own Chatwoot conversation while the AI has it,
    /// and posts the AI answer into Chatwoot.
    /// </summary>
    /// <param name="app">The pipeline. Add the door after the token check.</param>
    /// <param name="pattern">The public turn route's path.</param>
    /// <returns>The same pipeline.</returns>
    public static IApplicationBuilder UseChatwootTurn(this IApplicationBuilder app, string pattern)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return app.UseMiddleware<ChatwootTurnMiddleware>(pattern);
    }
}
