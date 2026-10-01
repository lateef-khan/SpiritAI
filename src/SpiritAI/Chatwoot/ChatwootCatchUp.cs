using AgentCore.Application.Conversation;
using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Before an AI turn, copies into the AgentCore copy the Chatwoot messages it missed.
/// </summary>
public sealed class ChatwootCatchUp(ChatwootClient chatwoot, IConversations conversations)
{
    /// <summary>How many pages are read back when the copy has no bookmark.</summary>
    public const int PagesWithNoBookmark = 2;

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

        // The staff part runs from the first staff message to the last; the customer's messages
        // after the last one wait in `after`, since the next staff message pulls them into it.
        var phase = new List<ChatwootMessage>();
        var after = new List<ChatwootMessage>();

        async Task AppendAsync(ChatMessage words)
        {
            var row = await conversations.AppendMessageAsync(copy.ConversationId, words, cancellationToken).ConfigureAwait(false);
            nextOrdinal = row.Ordinal + 1;
        }

        async Task EndPhaseAsync()
        {
            if (phase.Count > 0)
            {
                await AppendAsync(StaffPhaseNote.For(phase)).ConfigureAwait(false);
                phase.Clear();
            }

            foreach (var message in after)
            {
                await AppendAsync(new ChatMessage(ChatRole.User, message.Content)).ConfigureAwait(false);
            }

            after.Clear();
        }

        foreach (var message in read.Where(m => m.Id > (through ?? 0) && m.Id != turnMessageId && !string.IsNullOrEmpty(m.Content)).OrderBy(m => m.Id))
        {
            switch (message.SenderType)
            {
                case "user":
                    phase.AddRange(after);
                    after.Clear();
                    phase.Add(message);
                    break;

                case "contact" when phase.Count > 0:
                    after.Add(message);
                    break;

                case "contact":
                    await AppendAsync(new ChatMessage(ChatRole.User, message.Content)).ConfigureAwait(false);
                    break;

                // Spirit's own answers are in the copy already, unless a sweep dropped it.
                case "agent_bot" when through is null:
                    await EndPhaseAsync().ConfigureAwait(false);
                    await AppendAsync(new ChatMessage(ChatRole.Assistant, message.Content)).ConfigureAwait(false);
                    break;
            }
        }

        await EndPhaseAsync().ConfigureAwait(false);

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

        while (page.Count >= ChatwootApi.MessagePageSize
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
}
