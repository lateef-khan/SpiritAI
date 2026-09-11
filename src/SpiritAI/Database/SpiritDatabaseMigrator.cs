using AgentCore.Application.Ports;

using Microsoft.EntityFrameworkCore;

namespace SpiritAI.Database;

/// <summary>
/// Brings the <c>spirit</c> schema up to date while the host starts, before it serves a request.
/// </summary>
internal sealed class SpiritDatabaseMigrator(IServiceScopeFactory scopes) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        // spirit.handoff has a foreign key into public.call, and AgentCore creates that table on
        // its own schedule: the first time its call store is resolved. Resolving the store here
        // makes that happen first, so the table the key points at exists when the key is created.
        _ = scope.ServiceProvider.GetRequiredService<ICallStore>();

        var database = scope.ServiceProvider.GetRequiredService<SpiritDbContext>().Database;

        // Through the execution strategy, not a bare MigrateAsync: the boot connection is the one
        // most likely to find the database asleep, and the strategy is what retries it.
        await database
            .CreateExecutionStrategy()
            .ExecuteAsync(database, static (db, ct) => db.MigrateAsync(ct), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
