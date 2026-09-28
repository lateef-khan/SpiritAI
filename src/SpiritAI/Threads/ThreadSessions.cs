using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Mvc;

using SpiritAI.Hosting;

namespace SpiritAI.Threads;

/// <summary>The door in front of the Responses endpoint.</summary>
public static class ThreadSessionApplicationBuilderExtensions
{
    /// <summary>The route this guards when the host names none: the signed-in Responses route.</summary>
    public const string DefaultResponsesPattern = AgentCoreExtensions.ChatResponsesPattern;

    /// <summary>
    /// Lets a turn continue a thread the caller owns, and refuses one that names anybody else's.
    /// </summary>
    /// <param name="app">The application to add the door to. Add it after the token check.</param>
    /// <param name="pattern">The route to guard, or <see langword="null"/> for <see cref="DefaultResponsesPattern"/>.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseThreadSessions(this IApplicationBuilder app, string? pattern = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        pattern ??= DefaultResponsesPattern;
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return app.UseMiddleware<ThreadSessionMiddleware>(pattern);
    }
}

/// <summary>
/// Refuses a turn that names a thread the caller does not own. AgentCore opens an owned thread itself.
/// </summary>
internal sealed class ThreadSessionMiddleware(RequestDelegate next, string pattern)
{
    public async Task InvokeAsync(HttpContext context, IConversations conversations)
    {
        if (!context.Request.Path.StartsWithSegments(pattern, StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var namedThread = await TurnConversation.ReadAsync(context.Request).ConfigureAwait(false);

        // A turn that names no thread is a new conversation, and AgentCore mints the conversation for it.
        // Nothing here has an opinion about that.
        if (namedThread.Length == 0 || CallerPrincipal.KeyOf(context.User) is not { } key)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (await ThreadOwnership.ReadAsync(conversations, namedThread, key, context.RequestAborted).ConfigureAwait(false)
            is null)
        {
            await RefuseAsync(context, namedThread).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static async Task RefuseAsync(HttpContext context, string namedThread)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "No such thread.",
            Detail = $"This caller has no thread named '{namedThread}'.",
        }).ConfigureAwait(false);
    }
}
