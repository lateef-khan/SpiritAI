using SpiritAI.Contacts;

namespace SpiritAI.Tests.Contacts;

/// <summary>
/// An <see cref="IContactConversationStore"/> over a list, keeping the same promise the table does:
/// at most one row per conversation.
/// </summary>
internal sealed class FakeContactConversationStore : IContactConversationStore
{
    public List<(string ConversationId, long ContactId, ContactChannel Channel)> Rows { get; } = [];

    public Task EnsureAsync(string conversationId, long contactId, ContactChannel channel, CancellationToken cancellationToken)
    {
        if (!Rows.Any(row => row.ConversationId == conversationId))
        {
            Rows.Add((conversationId, contactId, channel));
        }

        return Task.CompletedTask;
    }

    public Task<long?> ContactIdOfAsync(string conversationId, CancellationToken cancellationToken)
    {
        foreach (var row in Rows)
        {
            if (row.ConversationId == conversationId)
            {
                return Task.FromResult<long?>(row.ContactId);
            }
        }

        return Task.FromResult<long?>(null);
    }

    /// <summary>The row added last wins: rows are added in the order the chats started.</summary>
    public Task<string?> LatestAsync(long contactId, ContactChannel channel, CancellationToken cancellationToken)
        => Task.FromResult<string?>(Rows.LastOrDefault(row => row.ContactId == contactId && row.Channel == channel).ConversationId);
}
