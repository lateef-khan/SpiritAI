using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>Puts <see cref="ChatwootCopyMiddleware"/> in the pipeline.</summary>
public static class ChatwootCopyDoor
{
    /// <summary>Copies widget chats into Chatwoot after each turn, or does nothing when the copy is not configured.</summary>
    /// <param name="app">The pipeline.</param>
    /// <param name="pattern">The public turn route's path, up to its first parameter.</param>
    /// <returns>The same pipeline.</returns>
    public static IApplicationBuilder UseChatwootCopy(this IApplicationBuilder app, string pattern)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return app.ApplicationServices.GetRequiredService<IOptions<ChatwootOptions>>().Value.CopyEnabled
            ? app.UseMiddleware<ChatwootCopyMiddleware>(pattern)
            : app;
    }
}
