using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Contacts;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="Contact"/> onto <c>spirit.contact</c>.</summary>
internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("contact");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
               .HasColumnName("id")
               .UseIdentityAlwaysColumn();

        builder.Property(c => c.DisplayName)
               .HasColumnName("display_name");

        builder.Property(c => c.CreatedAt)
               .HasColumnName("created_at")
               .HasDefaultValueSql("now()");

        builder.Property(c => c.ChatwootContactId)
               .HasColumnName("chatwoot_contact_id");

        builder.Property(c => c.ChatwootSourceId)
               .HasColumnName("chatwoot_source_id");
    }
}
