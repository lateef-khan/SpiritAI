using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;
using SpiritAI.Database.Configurations;

namespace SpiritAI.Contacts;

/// <summary>
/// The <see cref="IContactResolver"/> over <c>spirit.contact</c> and <c>spirit.contact_identity</c>,
/// through EF Core.
/// </summary>
public sealed class ContactResolver(SpiritDbContext database, TimeProvider clock) : IContactResolver
{
    /// <inheritdoc />
    public async Task<long> ResolveAsync(string channelKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelKey);

        var (kind, value) = ParseKey(channelKey);

        var contactId = await database.ContactIdentities
            .AsNoTracking()
            .Where(i => i.Kind == kind && i.Value == value)
            .Select(i => (long?)i.ContactId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? await CreateAsync(kind, value, cancellationToken).ConfigureAwait(false);

        return await FollowMergeAsync(contactId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes a contact and its first identity, or, when another request just made the same one,
    /// hands back the contact it made.
    /// </summary>
    private async Task<long> CreateAsync(ContactIdentityKind kind, string value, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var contact = new Contact { CreatedAt = now };

        database.Contacts.Add(contact);
        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var identity = new ContactIdentity
        {
            Kind = kind,
            Value = value,
            ContactId = contact.Id,
            Verified = kind == ContactIdentityKind.Visitor,
            FirstSeenAt = now,
        };

        database.ContactIdentities.Add(identity);

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException refused) when (IsSecondIdentity(refused))
        {
            // Left tracked, the context would try the insert again on its next save.
            database.Entry(identity).State = EntityState.Detached;

            return await database.ContactIdentities
                .AsNoTracking()
                .Where(i => i.Kind == kind && i.Value == value)
                .Select(i => i.ContactId)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return contact.Id;
    }

    /// <summary>Follows <see cref="Contact.MergedInto"/> to the surviving contact.</summary>
    private async Task<long> FollowMergeAsync(long contactId, CancellationToken cancellationToken)
    {
        var current = contactId;

        var seen = new HashSet<long> { current };

        while (await database.Contacts
                   .AsNoTracking()
                   .Where(c => c.Id == current)
                   .Select(c => c.MergedInto)
                   .SingleOrDefaultAsync(cancellationToken)
                   .ConfigureAwait(false)
               is { } next && seen.Add(next))
        {
            current = next;
        }

        return current;
    }

    /// <summary>Splits a channel key into the kind and value <c>contact_identity</c> keeps them as.</summary>
    private static (ContactIdentityKind Kind, string Value) ParseKey(string channelKey)
    {
        var separator = channelKey.IndexOf(':');

        if (separator <= 0 || separator == channelKey.Length - 1)
        {
            throw new ArgumentException(
                $"'{channelKey}' is not a channel key of the form '<kind>:<value>'.", nameof(channelKey));
        }

        var kindText = channelKey[..separator];
        
        var value = channelKey[(separator + 1)..];

        if (!Enum.TryParse<ContactIdentityKind>(kindText, true, out var kind))
        {
            throw new ArgumentException($"'{kindText}' is not a channel kind this host knows.", nameof(channelKey));
        }

        return (kind, value);
    }

    /// <summary>Whether the database refused a second identity for one key.</summary>
    private static bool IsSecondIdentity(DbUpdateException exception)
        => exception.InnerException is PostgresException { ConstraintName: ContactIdentityConfiguration.PrimaryKeyName };
}
