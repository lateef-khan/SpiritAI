using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="ChatwootLink"/> onto <c>spirit.chatwoot_link</c>.</summary>
internal sealed class ChatwootLinkConfiguration : IEntityTypeConfiguration<ChatwootLink>
{
    public void Configure(EntityTypeBuilder<ChatwootLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("chatwoot_link");

        builder.HasKey(l => l.ConversationId);

        builder.Property(l => l.ConversationId).HasColumnName("conversation_id");
        builder.Property(l => l.ChatwootConversationId).HasColumnName("chatwoot_conversation_id");
        builder.Property(l => l.CopiedThrough).HasColumnName("copied_through");
        builder.Property(l => l.AnnouncedHandoffId).HasColumnName("announced_handoff_id");
        builder.Property(l => l.NotedEmail).HasColumnName("noted_email");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // The cascade means AgentCore's retention sweep of agentcore.conversation cleans up after us.
        builder.HasOne<ConversationStub>()
            .WithMany()
            .HasForeignKey(l => l.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
