using System.Globalization;

using AgentCore.Application.Ports;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using SpiritAI.PublicChat;
using SpiritAI.Threads;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The door in front of the public turn route. It lets a turn run only on the visitor's own
/// Chatwoot conversation while the AI has it, makes and catches up the AI's copy first, and posts
/// the AI answer into Chatwoot once the visitor has it.
/// </summary>
internal sealed class ChatwootTurnMiddleware(RequestDelegate next, string pattern)
{
    public async Task InvokeAsync(
        HttpContext context,
        IOptions<ChatwootOptions> options,
        ChatwootClient chatwoot,
        IConversations conversations,
        ChatwootCatchUp catchUp,
        ChatwootTurn turn)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !context.Request.Path.StartsWithSegments(pattern, StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!options.Value.TurnEnabled)
        {
            await RefuseAsync(context, StatusCodes.Status503ServiceUnavailable, "Chatwoot is not set up.", "The AI answers only chats Chatwoot holds.")
                .ConfigureAwait(false);
            return;
        }

        var key = context.Request.Headers[VisitorPrincipal.Header].ToString();
        var namedChat = await TurnConversation.ReadAsync(context.Request).ConfigureAwait(false);

        if (!VisitorPrincipal.IsWellFormed(key)
            || IdOf(context, ChatwootTurn.ConversationHeader) is not { } displayId
            || IdOf(context, ChatwootTurn.MessageHeader) is not { } messageId
            || namedChat.Length == 0)
        {
            await RefuseAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Not a Chatwoot turn.",
                $"Send {VisitorPrincipal.Header}, {ChatwootTurn.ConversationHeader}, {ChatwootTurn.MessageHeader}, and the conversation in the body.")
                .ConfigureAwait(false);
            return;
        }

        var cancellationToken = context.RequestAborted;
        IReadOnlyList<ChatwootMessage>? newestPage;
        ChatwootConversation conversation;

        try
        {
            newestPage = await chatwoot.ListMessagesAsync(key, displayId, before: null, cancellationToken).ConfigureAwait(false);

            if (newestPage is null)
            {
                await RefuseAsync(context, StatusCodes.Status403Forbidden, "Not your chat.", "This visitor has no such Chatwoot conversation.")
                    .ConfigureAwait(false);
                return;
            }

            conversation = await chatwoot.GetConversationAsync(displayId, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            await RefuseAsync(context, StatusCodes.Status503ServiceUnavailable, "Chatwoot is not answering.", "Try again soon.")
                .ConfigureAwait(false);
            return;
        }

        if (!string.Equals(namedChat, ChatwootTurn.AgentCoreIdOf(conversation.Uuid), StringComparison.Ordinal))
        {
            await RefuseAsync(context, StatusCodes.Status403Forbidden, "Not your chat.", "The conversation does not match the Chatwoot conversation.")
                .ConfigureAwait(false);
            return;
        }

        if (!conversation.Pending)
        {
            await RefuseAsync(context, StatusCodes.Status409Conflict, "A person has this chat.", "Post the message to Chatwoot only.")
                .ConfigureAwait(false);
            return;
        }

        turn.Set(key, displayId, messageId);

        if (await conversations.GetAsync(namedChat, cancellationToken).ConfigureAwait(false) is null)
        {
            await conversations.CreateAsync(namedChat, cancellationToken).ConfigureAwait(false);
        }

        await catchUp.CatchUpAsync(namedChat, key, displayId, messageId, newestPage, cancellationToken).ConfigureAwait(false);

        var answerFrom = (await conversations.GetAsync(namedChat, cancellationToken).ConfigureAwait(false))!.NextOrdinal;

        context.Response.OnCompleted(() => ChatwootAnswer.PostInNewScopeAsync(context, namedChat, displayId, answerFrom));

        await next(context).ConfigureAwait(false);
    }

    private static int? IdOf(HttpContext context, string header)
        => int.TryParse(context.Request.Headers[header].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : null;

    private static async Task RefuseAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        }).ConfigureAwait(false);
    }
}
