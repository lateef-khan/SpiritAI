using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;

namespace SpiritAI.Database.Configurations;

/// <summary>
/// Maps <see cref="Role"/> onto <c>spirit.role</c>. The case-insensitive unique name is an
/// expression index the AccessModel migration makes in SQL; EF cannot describe it.
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role");

        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.BuiltIn).IsUnique().HasFilter("built_in");

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.Name).HasColumnName("name");
        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.BuiltIn).HasColumnName("built_in");
    }
}
