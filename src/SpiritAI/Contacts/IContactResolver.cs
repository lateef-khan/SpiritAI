namespace SpiritAI.Contacts;

/// <summary>
/// Turns a channel key into the id of the person it names.
/// </summary>
public interface IContactResolver
{
    /// <summary>
    /// Resolves a channel key, such as <c>visitor:abc123</c>, to a contact. A known key reads the
    /// identity and answers its <c>contact_id</c>. An unknown key makes a contact and the identity,
    /// then answers the new id.
    /// </summary>
    /// <param name="channelKey">The key, as <c>&lt;kind&gt;:&lt;value&gt;</c>.</param>
    /// <param name="cancellationToken">Cancels the read or write.</param>
    /// <returns>The id of the contact the key names.</returns>
    Task<long> ResolveAsync(string channelKey, CancellationToken cancellationToken);
}
