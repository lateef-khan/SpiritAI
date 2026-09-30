using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;
using SpiritAI.Hub;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="LinkedUser"/> onto <c>spirit.linked_user</c>.</summary>
internal sealed class LinkedUserConfiguration : IEntityTypeConfiguration<LinkedUser>
{
    public void Configure(EntityTypeBuilder<LinkedUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("linked_user", table =>
            table.HasCheckConstraint("ck_linked_user_app", "app IN ('desk', 'crm')"));

        builder.HasKey(l => new { l.UserId, l.App });
        builder.HasIndex(l => new { l.App, l.ExternalId }).IsUnique();

        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.Property(l => l.App).HasColumnName("app");
        builder.Property(l => l.ExternalId).HasColumnName("external_id");
        builder.Property(l => l.Ready).HasColumnName("ready").HasDefaultValue(false);

        // Deleting a Person in Neon Auth deletes their links.
        builder.HasOne<NeonUserStub>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
