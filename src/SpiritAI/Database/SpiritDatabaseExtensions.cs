using Microsoft.EntityFrameworkCore;

namespace SpiritAI.Database;

/// <summary>Registers the <c>spirit</c> schema and the migrator that puts it in place.</summary>
public static class SpiritDatabaseExtensions
{
    /// <summary>The ledger EF keeps of the migrations it has applied, in our schema and not AgentCore's.</summary>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// Adds <see cref="SpiritDbContext"/> over the database AgentCore uses, and migrates it at boot.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configuration">Where the connection string is read from; see <see cref="SpiritConnectionString"/>.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSpiritDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<SpiritDbContext>(options =>
            options.UseSpiritNpgsql(SpiritConnectionString.Read(configuration)));

        services.AddHostedService<SpiritDatabaseMigrator>();

        return services;
    }

    /// <summary>Points a context at PostgreSQL the way this host does everywhere, tests included.</summary>
    /// <param name="options">The options being built.</param>
    /// <param name="connectionString">The database, which is AgentCore's too.</param>
    /// <returns>The same builder.</returns>
    public static DbContextOptionsBuilder UseSpiritNpgsql(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        return options.UseNpgsql(
            connectionString,
            npgsql => npgsql
                .MigrationsHistoryTable(MigrationsHistoryTable, SpiritDbContext.Schema)
                .EnableRetryOnFailure());
    }
}
