using AgentCore.AspNetCore.Endpoints;

namespace SpiritAI.Access;

/// <summary>
/// Runs the entry of the caller's highest access group, and refuses a caller with no group.
/// </summary>
internal sealed class GroupEntrySelector : IEntrySelector
{
    /// <inheritdoc />
    public ValueTask<string?> SelectAsync(HttpContext http, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);

        return ValueTask.FromResult(
            AccessGroups.Highest(AccessGroups.Of(http.User)) is { } group ? AccessGroups.EntryOf(group) : null);
    }
}
