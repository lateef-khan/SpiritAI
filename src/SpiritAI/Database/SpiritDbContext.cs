using Microsoft.EntityFrameworkCore;

using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Reads;
using SpiritAI.RealTime.Presence;

namespace SpiritAI.Database;

/// <summary>
/// The <c>spirit</c> schema: every table this host owns, in the database AgentCore also uses.
/// </summary>
public sealed class SpiritDbContext(DbContextOptions<SpiritDbContext> options) : DbContext(options)
{
    /// <summary>The schema every table here lands in.</summary>
    public const string Schema = "spirit";

    /// <summary>Every request for a person, open and closed.</summary>
    public DbSet<Handoff> Handoffs => Set<Handoff>();

    /// <summary>How far each member of staff has read each chat.</summary>
    public DbSet<ConversationRead> ConversationReads => Set<ConversationRead>();

    /// <summary>Every open socket, whoever is on it.</summary>
    public DbSet<Presence> Presence => Set<Presence>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpiritDbContext).Assembly);
    }
}
