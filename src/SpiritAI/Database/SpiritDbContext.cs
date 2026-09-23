using Microsoft.EntityFrameworkCore;

using SpiritAI.Chatwoot;
using SpiritAI.Contacts;
using SpiritAI.Handoffs.Model;
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

    /// <summary>Every open socket, whoever is on it.</summary>
    public DbSet<Presence> Presence => Set<Presence>();

    /// <summary>Every person, one row each.</summary>
    public DbSet<Contact> Contacts => Set<Contact>();

    /// <summary>Every key that names a person.</summary>
    public DbSet<ContactIdentity> ContactIdentities => Set<ContactIdentity>();

    /// <summary>Which conversation belongs to which person.</summary>
    public DbSet<ContactConversation> ContactConversations => Set<ContactConversation>();

    /// <summary>The Chatwoot conversation each chat is copied into.</summary>
    public DbSet<ChatwootLink> ChatwootLinks => Set<ChatwootLink>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpiritDbContext).Assembly);
    }
}
