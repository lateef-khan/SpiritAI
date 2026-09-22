using SpiritAI.Contacts;

namespace SpiritAI.Tests.Contacts;

/// <summary>
/// An <see cref="IContactResolver"/> over a dictionary: a channel key seen before answers the same
/// id, an unseen one mints the next.
/// </summary>
internal sealed class FakeContactResolver : IContactResolver
{
    private readonly Dictionary<string, long> _contacts = new(StringComparer.Ordinal);
    private long _nextId = 1;

    public Task<long> ResolveAsync(string channelKey, CancellationToken cancellationToken)
    {
        if (!_contacts.TryGetValue(channelKey, out var contactId))
        {
            contactId = _nextId++;
            _contacts[channelKey] = contactId;
        }

        return Task.FromResult(contactId);
    }
}
