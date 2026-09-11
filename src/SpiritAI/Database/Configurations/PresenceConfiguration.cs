using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.RealTime.Presence;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="Presence"/> onto <c>spirit.presence</c>.</summary>
internal sealed class PresenceConfiguration : IEntityTypeConfiguration<Presence>
{
    /// <summary>The index the count and the sweep read: one kind, by when it was last seen.</summary>
    public const string KindSeenIndex = "presence_kind_seen_at";

    public void Configure(EntityTypeBuilder<Presence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("presence");

        builder.HasKey(p => p.ConnectionId);

        builder.Property(p => p.ConnectionId).HasColumnName("connection_id");
        builder.Property(p => p.CallerKey).HasColumnName("caller_key");
        builder.Property(p => p.CallerName).HasColumnName("caller_name");
        builder.Property(p => p.Kind).HasColumnName("kind");
        builder.Property(p => p.ConnectedAt).HasColumnName("connected_at").HasDefaultValueSql("now()");
        builder.Property(p => p.SeenAt).HasColumnName("seen_at").HasDefaultValueSql("now()");

        builder.HasIndex(p => new { p.Kind, p.SeenAt }).HasDatabaseName(KindSeenIndex);
    }
}
