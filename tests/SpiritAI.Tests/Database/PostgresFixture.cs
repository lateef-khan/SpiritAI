using AgentCore.Application.Configuration.Schema;
using AgentCore.Application.Ports;
using AgentCore.Application.Secrets;
using AgentCore.Infrastructure.Conversation.Postgres;
using AgentCore.Infrastructure.Database.Postgres;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using SpiritAI.Database;

using Xunit;

namespace SpiritAI.Tests.Database;

/// <summary>
/// The throwaway PostgreSQL <c>just db-up</c> provides, with the agentcore, neon_auth, and spirit schemas in place.
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

        // Neon creates neon_auth."user"; the throwaway database needs the copy before the migrations.
        await using (var neonAuth = _dataSource.CreateCommand(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Database", "neon-auth.sql"), TestContext.Current.CancellationToken)))
        {
            await neonAuth.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

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

    /// <summary>
    /// A database of its own, with the agentcore and neon_auth schemas and no migration applied, for
    /// a test that migrates step by step. Skips the calling test when there is no database.
    /// </summary>
    public async Task<ScratchDatabase> ScratchDatabaseAsync()
    {
        if (_connectionString is null)
        {
            Assert.Skip($"No database. Set {SecretVariable} to the output of `just db-url` to run this.");
        }

        return await ScratchDatabase.CreateAsync(_connectionString);
    }

    /// <summary>
    /// Opens AgentCore's own PostgreSQL conversation store on the database, the way the host does: through
    /// the <c>postgres</c> adapter, with the connection string handed over as the one secret it
    /// reads. Skips the calling test when there is no database.
    /// </summary>
    /// <returns>The store, which the caller disposes; it owns a pool of its own.</returns>
    public async Task<IConversationStore> OpenConversationStoreAsync()
    {
        if (_connectionString is null)
        {
            Assert.Skip($"No database. Set {SecretVariable} to the output of `just db-url` to run this.");
        }

        return await new PostgresConversationStoreAdapter().OpenAsync(
            new VendorProviderConfiguration { Kind = PostgresConversationStoreAdapter.ProviderKind },
            new OneSecret(KnownSecrets.PostgresConnectionStringName, _connectionString),
            TestContext.Current.CancellationToken);
    }

    /// <summary>Moves a conversation's last change back in time, as if nothing had touched it since.</summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="by">How far back.</param>
    public async Task AgeConversationAsync(string conversationId, TimeSpan by)
    {
        await using var update = Source.CreateCommand("UPDATE agentcore.conversation SET updated_at = updated_at - $2 WHERE conversation_id = $1");
        update.Parameters.AddWithValue(conversationId);
        update.Parameters.AddWithValue(by);
        await update.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Deletes a conversation.</summary>
    /// <param name="conversationId">The call to delete.</param>
    public async Task DeleteConversationAsync(string conversationId)
    {
        await using var delete = Source.CreateCommand("DELETE FROM agentcore.conversation WHERE conversation_id = $1");
        delete.Parameters.AddWithValue(conversationId);
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

    /// <summary>A resolver that holds one secret and nothing else.</summary>
    private sealed class OneSecret(string held, string value) : ISecretResolverPort
    {
        public ValueTask<string?> TryResolveAsync(string name, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(string.Equals(name, held, StringComparison.Ordinal) ? value : null);
    }
}
