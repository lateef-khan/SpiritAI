using SpiritAI.Access;

namespace SpiritAI.Tests.Access;

/// <summary>A resolver that gives every caller the same permissions, for hosts that do not read the database for access.</summary>
internal sealed class FixedAccess(params Permission[] held) : IAccessResolver
{
    public bool Banned { get; set; }

    public int Reads { get; private set; }

    public IReadOnlyList<Permission> Held { get; set; } = held;

    public ValueTask<PersonAccess> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Reads++;
        return ValueTask.FromResult(new PersonAccess(Banned, Held, Permissions.AgentOf(Held)));
    }
}
