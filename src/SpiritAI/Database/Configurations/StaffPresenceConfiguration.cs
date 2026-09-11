using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="StaffPresence"/> onto <c>spirit.staff_presence</c>.</summary>
internal sealed class StaffPresenceConfiguration : IEntityTypeConfiguration<StaffPresence>
{
    public void Configure(EntityTypeBuilder<StaffPresence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("staff_presence");

        builder.HasKey(p => p.ConnectionId);

        builder.Property(p => p.ConnectionId).HasColumnName("connection_id");
        builder.Property(p => p.StaffKey).HasColumnName("staff_key");
        builder.Property(p => p.StaffName).HasColumnName("staff_name");
        builder.Property(p => p.ConnectedAt).HasColumnName("connected_at").HasDefaultValueSql("now()");
        builder.Property(p => p.SeenAt).HasColumnName("seen_at").HasDefaultValueSql("now()");
    }
}
