using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="Role"/> onto <c>spirit.role</c>.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var groups = string.Join(", ", Enum.GetNames<AccessGroup>().Select(name => $"'{name}'"));

        builder.ToTable("role", table =>
        {
            table.HasCheckConstraint("role_access_group_check", $"access_group IN ({groups})");
        });

        builder.HasKey(r => r.Name);

        builder.Property(r => r.Name).HasColumnName("name");
        builder.Property(r => r.AccessGroup).HasColumnName("access_group");
    }
}
