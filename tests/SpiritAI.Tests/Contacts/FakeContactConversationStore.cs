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
}
