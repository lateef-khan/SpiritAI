using AgentCore.AspNetCore.Endpoints;

namespace SpiritAI.Access;

/// <summary>Runs the entry of the strongest chat agent the caller holds, and refuses a caller who holds none.</summary>
public sealed class AgentEntrySelector : IEntrySelector
{
    /// <inheritdoc />
    public ValueTask<string?> SelectAsync(HttpContext http, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);

        return ValueTask.FromResult(
            Permissions.AgentOf(Permissions.Of(http.User)) is { } agent ? Permissions.EntryOf(agent) : null);
    }
}
