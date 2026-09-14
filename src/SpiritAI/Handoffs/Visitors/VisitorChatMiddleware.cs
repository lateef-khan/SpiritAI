using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Mvc;

using SpiritAI.Handoffs.Store;
using SpiritAI.PublicChat;
using SpiritAI.Threads;

namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// The public counterpart of <c>ThreadSessionMiddleware</c>. A turn that names a chat is let
/// through once the chat is proved to be the visitor's and nobody has it; its session is reopened
/// when a reloaded widget carries on days later.
/// </summary>
/// <remarks>
/// A turn with no <see cref="VisitorPrincipal.Header"/> passes untouched. That is today's widget,
/// which sends none and keeps its session in memory; the rule that every public turn must carry a
/// key arrives with the widget spec, once there is a widget that can obey it.
/// </remarks>
internal sealed class VisitorChatMiddleware(RequestDelegate next, string pattern)
{
    public async Task InvokeAsync(HttpContext context, ICallStore calls, IHandoffStore handoffs, IThreadSessions sessions)
    {
        if (!context.Request.Path.StartsWithSegments(pattern, StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var sent = context.Request.Headers[VisitorPrincipal.Header].ToString();

        if (sent.Length == 0)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!VisitorPrincipal.IsWellFormed(sent))
        {
            await RefuseAsync(
                context,
                StatusCodes.Status400BadRequest,
                "No visitor key.",
                $"The {VisitorPrincipal.Header} header is not a key this host accepts.").ConfigureAwait(false);
            return;
        }

        var namedChat = await TurnConversation.ReadAsync(context.Request).ConfigureAwait(false);

        // A turn that names no chat is a new conversation, and AgentCore mints the call for it.
        // Nothing here has an opinion about that.
        if (namedChat.Length == 0)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var key = VisitorPrincipal.KeyOf(sent);

        if (await ThreadOwnership.ReadAsync(calls, namedChat, key, context.RequestAborted).ConfigureAwait(false) is null)
        {
            await RefuseAsync(
                context,
                StatusCodes.Status404NotFound,
                "No such thread.",
                $"This caller has no thread named '{namedChat}'.").ConfigureAwait(false);
            return;
        }

        if (await handoffs.OpenAsync(namedChat, context.RequestAborted).ConfigureAwait(false) is not null)
        {
            await RefuseAsync(
                context,
                StatusCodes.Status409Conflict,
                "A person has this chat.",
                "Send the message to the handoff route instead.",
                VisitorChatDoor.HandoffOpenType).ConfigureAwait(false);
            return;
        }

        if (!await sessions.IsLiveAsync(namedChat, context.RequestAborted).ConfigureAwait(false))
        {
            await sessions.ReopenAsync(namedChat, context.RequestAborted).ConfigureAwait(false);
        }

        await next(context).ConfigureAwait(false);
    }

    private static async Task RefuseAsync(HttpContext context, int statusCode, string title, string detail, string? type = null)
    {
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
        }).ConfigureAwait(false);
    }
}
