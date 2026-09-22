using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SpiritAI.Contacts;

/// <summary>Registers the contact resolver and the store that records a contact's conversations.</summary>
public static class ContactServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="IContactResolver"/> and <see cref="IContactConversationStore"/> over the
    /// <c>spirit</c> schema. Add it after <c>AddSpiritDatabase</c>.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContacts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IContactResolver, ContactResolver>();

        services.AddScoped<IContactConversationStore, ContactConversationStore>();

        return services;
    }
}
