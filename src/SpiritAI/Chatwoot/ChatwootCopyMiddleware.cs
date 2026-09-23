using SpiritAI.Threads;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Queues a widget chat for copying once its turn has run. By the time the rest of the pipeline
/// returns, AgentCore has stored both the visitor's words and the answer, streamed or not.
/// </summary>
internal sealed class ChatwootCopyMiddleware(RequestDelegate next, string pattern)
{
    public async Task InvokeAsync(HttpContext context, ChatwootCopyQueue queue)
    {
        if (!context.Request.Path.StartsWithSegments(pattern, StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // Read before the turn: the id is in the request body, and the response names it only in a
        // body this middleware never sees. The widget names it on every turn.
        var conversationId = await TurnConversation.ReadAsync(context.Request).ConfigureAwait(false);

        await next(context).ConfigureAwait(false);

        if (conversationId.Length > 0 && context.Response.StatusCode is >= 200 and < 300)
        {
            queue.Enqueue(conversationId);
        }
    }
}
