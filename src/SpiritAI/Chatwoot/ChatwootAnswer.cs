using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.Extensions.AI;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Posts the AI answer of one turn into Chatwoot as the bot, as Markdown. It runs once the visitor
/// has the whole answer, so it never slows the stream.
/// </summary>
public sealed class ChatwootAnswer(IConversations conversations, ChatwootClient chatwoot, ILogger<ChatwootAnswer> logger)
{
    /// <summary>
    /// Posts the answer from a scope of its own: the request's scope may be gone by then. Does
    /// nothing when the turn failed.
    /// </summary>
    /// <param name="context">The finished request.</param>
    /// <param name="conversationId">The AgentCore conversation.</param>
    /// <param name="chatwootConversationId">The Chatwoot conversation's display id.</param>
    /// <param name="fromOrdinal">The first ordinal the turn wrote.</param>
    public static async Task PostInNewScopeAsync(HttpContext context, string conversationId, int chatwootConversationId, int fromOrdinal)
    {
        if (context.Response.StatusCode is < 200 or >= 300)
        {
            return;
        }

        var services = context.RequestServices;
        var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ChatwootAnswer>()
            .PostAsync(conversationId, chatwootConversationId, fromOrdinal, stopping)
            .ConfigureAwait(false);
    }

    /// <summary>Posts each AI message the turn wrote, oldest first. A failed post is tried once more.</summary>
    /// <param name="conversationId">The AgentCore conversation.</param>
    /// <param name="chatwootConversationId">The Chatwoot conversation's display id.</param>
    /// <param name="fromOrdinal">The first ordinal the turn wrote.</param>
    /// <param name="cancellationToken">Cancels the posts.</param>
    public async Task PostAsync(string conversationId, int chatwootConversationId, int fromOrdinal, CancellationToken cancellationToken)
    {
        var stored = await conversations
            .LoadWindowAsync(conversationId, new TranscriptWindow(null, 1), cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return;
        }

        foreach (var row in stored.Messages.Where(m => m.Ordinal >= fromOrdinal).OrderBy(m => m.Ordinal))
        {
            if (row.Content.Role != ChatRole.Assistant || string.IsNullOrWhiteSpace(row.Content.Text))
            {
                continue;
            }

            var markdown = OpenUiMarkdown.ForStaff(row.Content.Text);

            if (!await TryPostAsync(chatwootConversationId, markdown, cancellationToken).ConfigureAwait(false)
                && !await TryPostAsync(chatwootConversationId, markdown, cancellationToken).ConfigureAwait(false))
            {
                logger.LogError(
                    "The AI answer of conversation {ConversationId} did not reach Chatwoot conversation {ChatwootConversationId}.",
                    conversationId,
                    chatwootConversationId);
            }
        }
    }

    private async Task<bool> TryPostAsync(int chatwootConversationId, string markdown, CancellationToken cancellationToken)
    {
        try
        {
            await chatwoot.PostMessageAsync(chatwootConversationId, markdown, fromVisitor: false, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (HttpRequestException failure)
        {
            logger.LogWarning(failure, "Posting an AI answer to Chatwoot conversation {ChatwootConversationId} failed.", chatwootConversationId);
            return false;
        }
    }
}
