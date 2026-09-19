using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="Handoff"/> onto <c>spirit.handoff</c>.</summary>
internal sealed class HandoffConfiguration : IEntityTypeConfiguration<Handoff>
{
    /// <summary>The unique index that allows one open row per conversation. A refused insert names it.</summary>
    public const string OpenPerConversationIndex = "handoff_open_per_conversation";

    /// <summary>The index the queue reads: open rows, oldest ask first.</summary>
    public const string QueueIndex = "handoff_queue";

    public void Configure(EntityTypeBuilder<Handoff> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("handoff", table =>
        {
            table.HasCheckConstraint("handoff_status_check", "status IN ('waiting', 'human', 'done')");
            table.HasCheckConstraint("handoff_asked_by_check", "asked_by IN ('bot', 'visitor')");
        });

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(h => h.ConversationId).HasColumnName("conversation_id");

        builder.Property(h => h.Status)
            .HasColumnName("status")
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                text => Enum.Parse<HandoffStatus>(text, true));

        builder.Property(h => h.AskedBy)
            .HasColumnName("asked_by")
            .HasConversion(
                askedBy => askedBy.ToString().ToLowerInvariant(),
                text => Enum.Parse<HandoffAskedBy>(text, true));

        builder.Property(h => h.Reason).HasColumnName("reason");
        builder.Property(h => h.AskedAt).HasColumnName("asked_at").HasDefaultValueSql("now()");
        builder.Property(h => h.AssigneeKey).HasColumnName("assignee_key");
        builder.Property(h => h.AssigneeName).HasColumnName("assignee_name");
        builder.Property(h => h.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(h => h.Email).HasColumnName("email");
        builder.Property(h => h.DoneAt).HasColumnName("done_at");

        // One open handoff per chat. Closed ones stay: they are the wait-time report.
        builder.HasIndex(h => h.ConversationId)
            .HasDatabaseName(OpenPerConversationIndex)
            .IsUnique()
            .HasFilter("status <> 'done'");

        builder.HasIndex(h => new { h.Status, h.AskedAt }).HasDatabaseName(QueueIndex);

        // The cascade means AgentCore's retention sweep of agentcore.conversation cleans up after us.
        builder.HasOne<ConversationStub>()
            .WithMany()
            .HasForeignKey(h => h.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
