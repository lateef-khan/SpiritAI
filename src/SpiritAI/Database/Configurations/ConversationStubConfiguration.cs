using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Database.Configurations;

/// <summary>
/// Points <see cref="ConversationStub"/> at AgentCore's <c>agentcore.conversation</c>, and keeps EF's hands off it.
/// </summary>
/// <remarks>
/// Excluded from migrations, so no migration ever creates, alters, or drops <c>conversation</c>; the table is
/// AgentCore's to migrate. The mapping is here only so the foreign key on <c>spirit.handoff</c> can
/// be declared.
/// </remarks>
internal sealed class ConversationStubConfiguration : IEntityTypeConfiguration<ConversationStub>
{
    public void Configure(EntityTypeBuilder<ConversationStub> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("conversation", "agentcore", table => table.ExcludeFromMigrations());

        builder.HasKey(c => c.ConversationId);

        builder.Property(c => c.ConversationId).HasColumnName("conversation_id");
    }
}
