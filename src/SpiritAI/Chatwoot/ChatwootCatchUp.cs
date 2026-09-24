using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Transcript;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Before an AI turn, copies into the AgentCore copy the Chatwoot messages it missed: staff
/// replies, what the visitor wrote while a person was in charge, or everything after a sweep.
/// </summary>
public sealed class ChatwootCatchUp(ChatwootClient chatwoot, IConversations conversations)
{
    /// <summary>How many messages Chatwoot puts on one page.</summary>
    public const int PageSize = 20;

    /// <summary>How many pages are read back when the copy has no bookmark.</summary>
    public const int PagesWithNoBookmark = 2;

    /// <summary>The team a staff message is signed with, under the name.</summary>
    public const string StaffDetail = "Support";

    /// <summary>Appends what the copy is missing, oldest first, then moves the bookmark.</summary>
    /// <param name="conversationId">The AgentCore conversation, which must exist.</param>
    /// <param name="sourceId">The visitor's key.</param>
    /// <param name="chatwootConversationId">The Chatwoot conversation's display id.</param>
    /// <param name="turnMessageId">The message the visitor just sent. The turn itself carries it.</param>
    /// <param name="newestPage">The newest page of messages, already read to prove the visitor owns the conversation.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task CatchUpAsync(
        string conversationId,
        string sourceId,
        int chatwootConversationId,
        int turnMessageId,
        IReadOnlyList<ChatwootMessage> newestPage,
        CancellationToken cancellationToken)
    {
        var record = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"There is no conversation {conversationId} to catch up.");

        var through = ChatwootBookmark.Read(record.Custom);

        var read = await ReadBackAsync(sourceId, chatwootConversationId, through, newestPage, cancellationToken).ConfigureAwait(false);

        if (read.Count == 0)
        {
            return;
        }

        foreach (var message in read.Where(m => m.Id > (through ?? 0) && m.Id != turnMessageId).OrderBy(m => m.Id))
        {
            if (Copy(message, rebuilding: through is null) is { } copy)
            {
                await conversations.AppendMessageAsync(conversationId, copy, cancellationToken).ConfigureAwait(false);
            }
        }

        var newest = read.Max(m => m.Id);

        if (newest != through)
        {
            await conversations.SetCustomAsync(conversationId, ChatwootBookmark.Write(record.Custom, newest), cancellationToken)
                .ConfigureAwait(false);
        }
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
