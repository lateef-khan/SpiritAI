using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Contacts;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="ContactIdentity"/> onto <c>spirit.contact_identity</c>.</summary>
internal sealed class ContactIdentityConfiguration : IEntityTypeConfiguration<ContactIdentity>
{
    /// <summary>The primary key's name. A refused insert names it when two requests race to make the same key.</summary>
    public const string PrimaryKeyName = "PK_contact_identity";

    /// <summary>The index a contact's identities are read back through.</summary>
    public const string ContactIndex = "contact_identity_contact_id";

    public void Configure(EntityTypeBuilder<ContactIdentity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("contact_identity", table =>
        {
            table.HasCheckConstraint("contact_identity_kind_check", "kind IN ('visitor', 'phone', 'email')");
        });

        builder.HasKey(i => new { i.Kind, i.Value }).HasName(PrimaryKeyName);

        builder.Property(i => i.Kind)
               .HasColumnName("kind")
               .HasConversion(
                    kind => kind.ToString().ToLowerInvariant(),
                    text => Enum.Parse<ContactIdentityKind>(text, true));

        builder.Property(i => i.Value)
               .HasColumnName("value");

        builder.Property(i => i.ContactId)
               .HasColumnName("contact_id");

        builder.Property(i => i.Verified)
               .HasColumnName("verified");

        builder.Property(i => i.FirstSeenAt)
               .HasColumnName("first_seen_at")
               .HasDefaultValueSql("now()");

        builder.HasIndex(i => i.ContactId)
               .HasDatabaseName(ContactIndex);

        builder.HasOne<Contact>()
               .WithMany()
               .HasForeignKey(i => i.ContactId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
