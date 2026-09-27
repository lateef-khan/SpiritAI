using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="UserRole"/> onto <c>spirit.user_role</c>.</summary>
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user_role");

        builder.HasKey(r => new { r.UserId, r.Role });

        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.Property(r => r.Role).HasColumnName("role");

        // Deleting a person in Neon Auth deletes their roles.
        builder.HasOne<NeonUserStub>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(r => r.Role)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
