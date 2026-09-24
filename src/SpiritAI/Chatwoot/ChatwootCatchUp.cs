using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Before an AI turn, copies into the AgentCore copy the Chatwoot messages it missed.
/// </summary>
public sealed class ChatwootCatchUp(ChatwootClient chatwoot, IConversations conversations)
{
    /// <summary>How many messages Chatwoot puts on one page.</summary>
    public const int PageSize = 20;

    /// <summary>How many pages are read back when the copy has no bookmark.</summary>
    public const int PagesWithNoBookmark = 2;

    /// <summary>The team a staff message is signed with, under the name.</summary>
    public const string StaffDetail = "Support";

    /// <summary>
    /// Appends what the copy is missing, oldest first, then files the Chatwoot ids and moves the
    /// bookmark in one write.
    /// </summary>
    /// <param name="copy">The AgentCore conversation, as read just before the turn.</param>
    /// <param name="ids">The Chatwoot conversation and the visitor's key.</param>
    /// <param name="turnMessageId">The message the visitor just sent. The turn itself carries it.</param>
    /// <param name="newestPage">The newest page of messages, already read to prove the visitor owns the conversation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The ordinal the turn's first row takes.</returns>
    public async Task<int> CatchUpAsync(
        ConversationRecord copy,
        ChatwootIds ids,
        int turnMessageId,
        IReadOnlyList<ChatwootMessage> newestPage,
        CancellationToken cancellationToken)
    {
        var through = ChatwootBookmark.Read(copy.Custom);
        var nextOrdinal = copy.NextOrdinal;

        var read = await ReadBackAsync(ids.VisitorKey, ids.ConversationId, through, newestPage, cancellationToken).ConfigureAwait(false);

        foreach (var message in read.Where(m => m.Id > (through ?? 0) && m.Id != turnMessageId).OrderBy(m => m.Id))
        {
            if (Copy(message, rebuilding: through is null) is { } words)
            {
                var row = await conversations.AppendMessageAsync(copy.ConversationId, words, cancellationToken).ConfigureAwait(false);
                nextOrdinal = row.Ordinal + 1;
            }
        }

        var newest = read.Count == 0 ? through : read.Max(m => m.Id);

        if (ChatwootIds.Read(copy.Custom) != ids || newest != through)
        {
            var custom = ids.Write(copy.Custom);

            if (newest is { } bookmark)
            {
                custom = ChatwootBookmark.Write(custom, bookmark);
            }

            await conversations.SetCustomAsync(copy.ConversationId, custom, cancellationToken).ConfigureAwait(false);
        }

        return nextOrdinal;
    }

    /// <summary>
    /// Pages back from <paramref name="newestPage"/> until a page reaches the bookmark, or, with no
    /// bookmark, until <see cref="PagesWithNoBookmark"/> pages are read.
    /// </summary>
    private async Task<List<ChatwootMessage>> ReadBackAsync(
        string sourceId, int chatwootConversationId, int? through, IReadOnlyList<ChatwootMessage> newestPage, CancellationToken cancellationToken)
    {
        var read = new List<ChatwootMessage>(newestPage);
        var page = newestPage;
        var pages = 1;

        while (page.Count >= PageSize
            && (through is { } bookmark ? page.Min(m => m.Id) > bookmark : pages < PagesWithNoBookmark))
        {
            page = await chatwoot
                .ListMessagesAsync(sourceId, chatwootConversationId, before: page.Min(m => m.Id), cancellationToken)
                .ConfigureAwait(false) ?? [];

            read.AddRange(page);
            pages++;
        }

        return read;
    }

    /// <summary>
    /// The message as the copy keeps it. Spirit's own bot answers are in the copy already, so they
    /// are copied only when <paramref name="rebuilding"/> the copy after a sweep.
    /// </summary>
    private static ChatMessage? Copy(ChatwootMessage message, bool rebuilding)
    {
        if (string.IsNullOrEmpty(message.Content))
        {
            return null;
        }

        switch (message.SenderType)
        {
            case "contact":
                return new ChatMessage(ChatRole.User, message.Content);

            case "user":
                var staff = new ChatMessage(ChatRole.Assistant, message.Content);
                SpeakerProperty.Attach(staff, HandoffSpeaker.Human(message.SenderName ?? StaffDetail, StaffDetail));
                return staff;

            case "agent_bot" when rebuilding:
                return new ChatMessage(ChatRole.Assistant, message.Content);

            default:
                return null;
        }
    }
}
