using Microsoft.EntityFrameworkCore;

using SpiritAI.Access;
using SpiritAI.Hub;

namespace SpiritAI.Database;

/// <summary>
/// The <c>spirit</c> schema: every table this host owns, in the database AgentCore also uses.
/// </summary>
public sealed class SpiritDbContext(DbContextOptions<SpiritDbContext> options) : DbContext(options)
{
    /// <summary>The schema every table here lands in.</summary>
    public const string Schema = "spirit";

    /// <summary>Every role.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>The permissions each role but the built-in one grants.</summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>Which Neon Auth user holds which role.</summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>Who is banned.</summary>
    public DbSet<PersonBan> PersonBans => Set<PersonBan>();

    /// <summary>Which Person has which Desk or CRM user.</summary>
    public DbSet<LinkedUser> LinkedUsers => Set<LinkedUser>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpiritDbContext).Assembly);
    }
}
