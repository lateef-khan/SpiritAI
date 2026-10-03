using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="RolePermission"/> onto <c>spirit.role_permission</c>. No check constraint: the code list is the truth.</summary>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_permission");

        builder.HasKey(p => new { p.RoleId, p.Key });

        builder.Property(p => p.RoleId).HasColumnName("role_id");
        builder.Property(p => p.Key).HasColumnName("permission");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
