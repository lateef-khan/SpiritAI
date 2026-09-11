using AgentCore.Infrastructure.Database.Postgres;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;

using Xunit;

namespace SpiritAI.Tests.Database;

/// <summary>
/// The throwaway PostgreSQL <c>just db-up</c> provides, with both schemas in place.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>The variable the justfile sets: the configuration key in its environment spelling.</summary>
    public const string SecretVariable = "AgentCore__Secrets__postgres-connection-string";

    /// <summary>The variable read when <see cref="SecretVariable"/> is unset.</summary>
    public const string PlainVariable = "POSTGRES_CONNECTION_STRING";

    private readonly string? _connectionString =
        Present(Environment.GetEnvironmentVariable(SecretVariable))
        ?? Present(Environment.GetEnvironmentVariable(PlainVariable));

    private NpgsqlDataSource? _dataSource;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }

        _dataSource = NpgsqlDataSource.Create(_connectionString);

        await PostgresSchema.ApplyAsync(_dataSource, TestContext.Current.CancellationToken);

        await using var database = Open();
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Opens a context on the database, or skips the calling test when there is none.</summary>
    /// <returns>A context the caller disposes.</returns>
    public SpiritDbContext Open()
    {
        if (_connectionString is null)
        {
            Assert.Skip($"No database. Set {SecretVariable} to the output of `just db-url` to run this.");
        }

        var options = new DbContextOptionsBuilder<SpiritDbContext>();
        options.UseSpiritNpgsql(_connectionString);

        return new SpiritDbContext(options.Options);
    }

    /// <summary>Writes a row into AgentCore's <c>public.call</c> for a handoff to point at.</summary>
    /// <param name="callId">The call to make. Every other column has a default.</param>
    public async Task MakeCallAsync(string callId)
    {
        await using var insert = Source.CreateCommand("INSERT INTO public.call (call_id) VALUES ($1)");
        insert.Parameters.AddWithValue(callId);
        await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Deletes a call, and by the cascade every handoff row that pointed at it.</summary>
    /// <param name="callId">The call to delete.</param>
    public async Task DeleteCallAsync(string callId)
    {
        await using var delete = Source.CreateCommand("DELETE FROM public.call WHERE call_id = $1");
        delete.Parameters.AddWithValue(callId);
        await delete.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    private NpgsqlDataSource Source
    {
        get
        {
            if (_dataSource is null)
            {
                Assert.Skip($"No database. Set {SecretVariable} to the output of `just db-url` to run this.");
            }

            return _dataSource;
        }
    }

    private static string? Present(string? value) => value is { Length: > 0 } ? value : null;
}
