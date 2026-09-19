using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Reads;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="ConversationRead"/> onto <c>spirit.conversation_read</c>.</summary>
internal sealed class ConversationReadConfiguration : IEntityTypeConfiguration<ConversationRead>
{
    public void Configure(EntityTypeBuilder<ConversationRead> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("conversation_read");

        builder.HasKey(r => new { r.ConversationId, r.StaffKey });

        builder.Property(r => r.ConversationId).HasColumnName("conversation_id");
        builder.Property(r => r.StaffKey).HasColumnName("staff_key");
        builder.Property(r => r.SeenOrdinal).HasColumnName("seen_ordinal");
        builder.Property(r => r.SeenAt).HasColumnName("seen_at").HasDefaultValueSql("now()");

        // The cascade means AgentCore's retention sweep of agentcore.conversation cleans up after us.
        builder.HasOne<ConversationStub>()
            .WithMany()
            .HasForeignKey(r => r.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
