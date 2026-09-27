using AgentCore.Application.Configuration.Schema;

namespace SpiritAI.Access;

/// <summary>
/// Fails the start when an access group runs an entry <c>spirit.yaml</c> does not declare.
/// </summary>
internal sealed class AccessEntryCheck(IServiceProvider services) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var configuration = services.GetRequiredService<AgentCoreConfiguration>();

        var missing = AccessGroups.Entries.Where(entry => !configuration.Entries.ContainsKey(entry)).ToList();

        return missing.Count == 0
            ? Task.CompletedTask
            : throw new InvalidOperationException(
                $"spirit.yaml declares no entry named {string.Join(", ", missing)}, which an access group runs. "
                + "Add it under entries:.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
