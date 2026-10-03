using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="PersonBan"/> onto <c>spirit.person_ban</c>.</summary>
internal sealed class PersonBanConfiguration : IEntityTypeConfiguration<PersonBan>
{
    public void Configure(EntityTypeBuilder<PersonBan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("person_ban");

        builder.HasKey(b => b.UserId);

        builder.Property(b => b.UserId).HasColumnName("user_id");
        builder.Property(b => b.BannedAt).HasColumnName("banned_at");
        builder.Property(b => b.BannedBy).HasColumnName("banned_by");
        builder.Property(b => b.Reason).HasColumnName("reason");

        builder.HasOne<NeonUserStub>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
