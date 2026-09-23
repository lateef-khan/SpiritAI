using System.Globalization;
using System.Net;

using AgentCore.Application.Ports;
using AgentCore.Application.Transcript;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

using SpiritAI.Contacts;
using SpiritAI.Database;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Copies one widget chat into Chatwoot, so staff can read every chat, not only the ones handed
/// to them. The visitor's words go in as the contact's, the AI's as the bot's. Staff words and the
/// host's own lines carry a speaker and are skipped: staff words came from Chatwoot already.
/// After the words, an open handoff is told to Chatwoot, so staff read what led up to it.
/// </summary>
public sealed class ChatwootCopy(
    SpiritDbContext database,
    IConversations conversations,
    IContactConversationStore contactConversations,
    ChatwootClient chatwoot,
    IHandoffStore handoffs,
    ChatwootHandoffNotice notice,
    TimeProvider clock)
{
    /// <summary>
    /// How many turns back a copy reads. A chat that fell further behind, because Chatwoot was down
    /// for that long, has its older turns skipped.
    /// </summary>
    public const int TurnsRead = 5;

    /// <summary>What <see cref="ChatwootLink.CopiedThrough"/> starts at: before the first ordinal.</summary>
    public const int NothingCopied = -1;

    /// <summary>Copies what the chat holds past its last copy.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="cancellationToken">Cancels the copy. What was posted stays counted.</param>
    public async Task CopyAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var stored = await conversations
            .LoadWindowAsync(conversationId, new TranscriptWindow(null, TurnsRead), cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return;
        }

        var link = await database.ChatwootLinks
            .SingleOrDefaultAsync(l => l.ConversationId == conversationId, cancellationToken)
            .ConfigureAwait(false);

        var copiedThrough = link?.CopiedThrough ?? NothingCopied;

        foreach (var row in stored.Messages.Where(m => m.Ordinal > copiedThrough).OrderBy(m => m.Ordinal))
        {
            if (Post(row.Content) is { } post)
            {
                link ??= await OpenAsync(conversationId, cancellationToken).ConfigureAwait(false);

                try
                {
                    await chatwoot.PostMessageAsync(link.ChatwootConversationId, post.Content, post.FromVisitor, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException gone) when (gone.StatusCode == HttpStatusCode.NotFound)
                {
                    // Staff deleted the conversation in Chatwoot. The chat carries on in a new one.
                    link.ChatwootConversationId = await OpenConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);
                    await chatwoot.PostMessageAsync(link.ChatwootConversationId, post.Content, post.FromVisitor, cancellationToken).ConfigureAwait(false);
                }
            }

            if (link is not null)
            {
                link.CopiedThrough = row.Ordinal;
                link.UpdatedAt = clock.GetUtcNow();
                await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (await handoffs.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false) is { } open)
        {
            link ??= await OpenAsync(conversationId, cancellationToken).ConfigureAwait(false);

            await notice.TellAsync(link, open, cancellationToken).ConfigureAwait(false);

            link.UpdatedAt = clock.GetUtcNow();
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>What a stored message becomes in Chatwoot, or <see langword="null"/> when it is not copied.</summary>
    internal static (string Content, bool FromVisitor)? Post(ChatMessage message)
    {
        if (SpeakerProperty.Read(message) is not null || string.IsNullOrWhiteSpace(message.Text))
        {
            return null;
        }

        if (message.Role == ChatRole.User)
        {
            return (message.Text, true);
        }

        if (message.Role == ChatRole.Assistant)
        {
            return (OpenUiText.ForStaff(message.Text), false);
        }

        return null;
    }

    /// <summary>Opens the chat's Chatwoot conversation and records it.</summary>
    private async Task<ChatwootLink> OpenAsync(string conversationId, CancellationToken cancellationToken)
    {
        var link = new ChatwootLink
        {
            ConversationId = conversationId,
            ChatwootConversationId = await OpenConversationAsync(conversationId, cancellationToken).ConfigureAwait(false),
            CopiedThrough = NothingCopied,
            UpdatedAt = clock.GetUtcNow(),
        };

        database.ChatwootLinks.Add(link);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return link;
    }

    /// <summary>
    /// Opens a Chatwoot conversation for the chat, under the chat's contact. One chat keeps one
    /// conversation: the bot is connected to the inbox, so Chatwoot reopens a resolved one as
    /// pending, the bot's, when the visitor writes again (<c>Message#reopen_resolved_conversation</c>).
    /// </summary>
    private async Task<int> OpenConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        var contactId = await contactConversations.ContactIdOfAsync(conversationId, cancellationToken).ConfigureAwait(false);

        var sourceId = contactId is { } id
            ? await SourceIdOfAsync(id, cancellationToken).ConfigureAwait(false)
            : (await chatwoot.CreateContactAsync("Visitor", cancellationToken).ConfigureAwait(false)).SourceId;

        return await chatwoot.CreateConversationAsync(sourceId, conversationId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The contact's key in the Spirit inbox, made in Chatwoot on the contact's first copy and kept
    /// on the contact. Only the first save counts: when a second copy made its own Chatwoot contact
    /// at the same time, the saved key wins and the other contact stays empty.
    /// </summary>
    private async Task<string> SourceIdOfAsync(long contactId, CancellationToken cancellationToken)
    {
        if (await SavedSourceIdAsync(contactId, cancellationToken).ConfigureAwait(false) is { } saved)
        {
            return saved;
        }

        var made = await chatwoot
            .CreateContactAsync("Visitor " + contactId.ToString(CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);

        var claimed = await database.Contacts
            .Where(c => c.Id == contactId && c.ChatwootSourceId == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.ChatwootContactId, made.Id)
                    .SetProperty(c => c.ChatwootSourceId, made.SourceId),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 1
            ? made.SourceId
            : await SavedSourceIdAsync(contactId, cancellationToken).ConfigureAwait(false) ?? made.SourceId;
    }

    private Task<string?> SavedSourceIdAsync(long contactId, CancellationToken cancellationToken)
        => database.Contacts
            .AsNoTracking()
            .Where(c => c.Id == contactId)
            .Select(c => c.ChatwootSourceId)
            .SingleOrDefaultAsync(cancellationToken);
}
