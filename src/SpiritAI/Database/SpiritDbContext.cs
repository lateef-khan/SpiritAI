using Microsoft.EntityFrameworkCore;

namespace SpiritAI.Database;

/// <summary>
/// The <c>spirit</c> schema, in the database AgentCore also uses. It holds no tables: Chatwoot keeps
/// what they held. The context stays so the schema's migrations still run at boot.
/// </summary>
public sealed class SpiritDbContext(DbContextOptions<SpiritDbContext> options) : DbContext(options)
{
    /// <summary>The schema every table here lands in.</summary>
    public const string Schema = "spirit";

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
    }
}
