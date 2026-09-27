using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Access;

namespace SpiritAI.Database.Configurations;

/// <summary>
/// Points <see cref="NeonUserStub"/> at Neon Auth's <c>neon_auth."user"</c>, and keeps EF's hands off it.
/// </summary>
internal sealed class NeonUserStubConfiguration : IEntityTypeConfiguration<NeonUserStub>
{
    public void Configure(EntityTypeBuilder<NeonUserStub> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("user", "neon_auth", table => table.ExcludeFromMigrations());

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
    }
}
