using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Mvc;

namespace SpiritAI.Threads;

/// <summary>
/// Whether a call has a live session, and how to give it one.
/// </summary>
public interface IThreadSessions
{
    /// <summary>Whether this host is already holding a session for one call.</summary>
    /// <param name="callId">The call to ask about.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    ValueTask<bool> IsLiveAsync(string callId, CancellationToken cancellationToken = default);

    /// <summary>Opens a session for a call that already exists.</summary>
    /// <param name="callId">The call to pick up again.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    ValueTask ReopenAsync(string callId, CancellationToken cancellationToken = default);
}

/// <summary>The two answers, from AgentCore's own session table.</summary>
internal sealed class AgentCoreThreadSessions(ICallSessions sessions) : IThreadSessions
{
    /// <inheritdoc />
    public async ValueTask<bool> IsLiveAsync(string callId, CancellationToken cancellationToken = default)
        => await sessions.TryGetAsync(callId, cancellationToken).ConfigureAwait(false) is not null;

    /// <inheritdoc />
    public async ValueTask ReopenAsync(string callId, CancellationToken cancellationToken = default)
        => await sessions.OpenAsync(callId, cancellationToken).ConfigureAwait(false);
}

/// <summary>Registers the seam the chat door reads.</summary>
public static class ThreadSessionServiceCollectionExtensions
{
    /// <summary>Adds the default <see cref="IThreadSessions"/>, over AgentCore's session table.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddThreadSessions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IThreadSessions, AgentCoreThreadSessions>();

        return services;
    }
}

/// <summary>The door in front of the chat endpoint.</summary>
public static class ThreadSessionApplicationBuilderExtensions
{
    /// <summary>The route this guards when the host names none.</summary>
    public const string DefaultChatPattern = "/v1/chat/completions";

    /// <summary>
    /// Lets a turn continue a thread the caller owns, and refuses one that names anybody else's.
    /// </summary>
    /// <param name="app">The application to add the door to. Add it after the token check.</param>
    /// <param name="pattern">The route to guard.</param>
    /// <returns>The same application.</returns>
    public static IApplicationBuilder UseThreadSessions(
        this IApplicationBuilder app, string pattern = DefaultChatPattern)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        return app.UseMiddleware<ThreadSessionMiddleware>(pattern);
    }
}

/// <summary>
/// Opens a session for a turn that names a thread, once the thread is proved to be the caller's.
/// </summary>
internal sealed class ThreadSessionMiddleware(RequestDelegate next, string pattern)
{
    /// <summary>The header a turn names its thread in. AgentCore's own.</summary>
    public const string SessionHeaderName = "X-AgentCore-Session";

    public async Task InvokeAsync(HttpContext context, ICallStore calls, IThreadSessions sessions)
    {
        if (!context.Request.Path.StartsWithSegments(pattern, StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var namedThread = context.Request.Headers[SessionHeaderName].ToString();

        // A turn that names no thread is a new conversation, and AgentCore mints the call for it.
        // Nothing here has an opinion about that.
        if (namedThread.Length == 0 || CallerPrincipal.KeyOf(context.User) is not { } key)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (await ThreadOwnership.ReadAsync(calls, namedThread, key, context.RequestAborted).ConfigureAwait(false)
            is null)
        {
            await RefuseAsync(context, namedThread).ConfigureAwait(false);
            return;
        }

        if (!await sessions.IsLiveAsync(namedThread, context.RequestAborted).ConfigureAwait(false))
        {
            await sessions.ReopenAsync(namedThread, context.RequestAborted).ConfigureAwait(false);
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
