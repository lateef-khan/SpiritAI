using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;
using SpiritAI.Database.Configurations;

namespace SpiritAI.Contacts;

/// <summary>
/// The <see cref="IContactConversationStore"/> over <c>spirit.contact_conversation</c>, through EF Core.
/// </summary>
public sealed class ContactConversationStore(SpiritDbContext database, TimeProvider clock) : IContactConversationStore
{
    /// <inheritdoc />
    public async Task EnsureAsync(string conversationId, long contactId, ContactChannel channel, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        if (await database.ContactConversations
                .AsNoTracking()
                .AnyAsync(c => c.ConversationId == conversationId, cancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        var row = new ContactConversation
        {
            ConversationId = conversationId,
            ContactId = contactId,
            Channel = channel,
            StartedAt = clock.GetUtcNow(),
        };

        database.ContactConversations.Add(row);

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException refused) when (IsAlreadyThere(refused))
        {
            database.Entry(row).State = EntityState.Detached;
        }
    }

    /// <inheritdoc />
    public Task<long?> ContactIdOfAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        return database.ContactConversations
            .AsNoTracking()
            .Where(c => c.ConversationId == conversationId)
            .Select(c => (long?)c.ContactId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<string?> LatestAsync(long contactId, ContactChannel channel, CancellationToken cancellationToken)
        => database.ContactConversations
            .AsNoTracking()
            .Where(c => c.ContactId == contactId && c.Channel == channel)
            .OrderByDescending(c => c.StartedAt)
            .Select(c => c.ConversationId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Whether the database refused a second row for one conversation.</summary>
    private static bool IsAlreadyThere(DbUpdateException exception)
        => exception.InnerException is PostgresException { ConstraintName: ContactConversationConfiguration.PrimaryKeyName };
}
