using Xunit;

namespace SpiritAI.Tests.Database;

/// <summary>
/// Every test class that touches the database. They share one <see cref="PostgresFixture"/> and
/// run one after another: two migrations at once on an empty database collide.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>The name a test class joins with <c>[Collection(PostgresCollection.Name)]</c>.</summary>
    public const string Name = "Postgres";
}
