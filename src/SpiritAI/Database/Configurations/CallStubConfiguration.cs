using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Handoffs.Model;

namespace SpiritAI.Database.Configurations;

/// <summary>
/// Points <see cref="CallStub"/> at AgentCore's <c>public.call</c>, and keeps EF's hands off it.
/// </summary>
/// <remarks>
/// Excluded from migrations, so no migration ever creates, alters, or drops <c>call</c>; the table is
/// AgentCore's to migrate. The mapping is here only so the foreign key on <c>spirit.handoff</c> can
/// be declared.
/// </remarks>
internal sealed class CallStubConfiguration : IEntityTypeConfiguration<CallStub>
{
    public void Configure(EntityTypeBuilder<CallStub> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("call", "public", table => table.ExcludeFromMigrations());

        builder.HasKey(c => c.CallId);

        builder.Property(c => c.CallId).HasColumnName("call_id");
    }
}
