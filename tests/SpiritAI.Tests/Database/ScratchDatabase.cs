using AgentCore.Infrastructure.Database.Postgres;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;

using Xunit;

namespace SpiritAI.Tests.Database;

/// <summary>A throwaway database next to the fixture's, dropped on dispose.</summary>
public sealed class ScratchDatabase : IAsyncDisposable
{
    private readonly string _admin;
    private readonly string _name;

    private ScratchDatabase(string admin, string name, string connectionString)
    {
        _admin = admin;
        _name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<ScratchDatabase> CreateAsync(string admin)
    {
        var name = $"spirit_scratch_{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var server = new NpgsqlConnection(admin))
        {
            await server.OpenAsync(cancellationToken);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", server);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var connectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;

        await using (var source = NpgsqlDataSource.Create(connectionString))
        {
            await PostgresSchema.ApplyAsync(source, cancellationToken);
            await using var neonAuth = source.CreateCommand(
                await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "neon-auth.sql"), cancellationToken));
            await neonAuth.ExecuteNonQueryAsync(cancellationToken);
        }

        return new ScratchDatabase(admin, name, connectionString);
    }

    public SpiritDbContext Open()
    {
        var options = new DbContextOptionsBuilder<SpiritDbContext>();
        options.UseSpiritNpgsql(ConnectionString);
        return new SpiritDbContext(options.Options);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var server = new NpgsqlConnection(_admin);
        await server.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", server);
        await drop.ExecuteNonQueryAsync();
    }
}
