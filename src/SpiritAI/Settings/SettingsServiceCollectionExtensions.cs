namespace SpiritAI.Settings;

/// <summary>Registers the Settings page's services.</summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>Adds the People reads and writes. Needs <c>AddHub</c>, <c>AddAccess</c> and <c>AddNeon</c> first.</summary>
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<People>();
        services.AddScoped<Roles>();
        services.AddScoped<PersonRemoval>();
        services.AddScoped<PersonAdding>();
        services.AddScoped<BanDesk>();

        return services;
    }
}
