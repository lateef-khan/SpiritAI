using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SpiritAI.Contacts;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Database.Configurations;

/// <summary>Maps <see cref="ContactConversation"/> onto <c>spirit.contact_conversation</c>.</summary>
internal sealed class ContactConversationConfiguration : IEntityTypeConfiguration<ContactConversation>
{
    /// <summary>The primary key's name. A refused insert names it when two requests race to record one chat.</summary>
    public const string PrimaryKeyName = "PK_contact_conversation";

    /// <summary>The index a contact's conversations are read back through, newest first.</summary>
    public const string ContactStartedIndex = "contact_conversation_contact_id_started_at";

    public void Configure(EntityTypeBuilder<ContactConversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("contact_conversation", table =>
        {
            table.HasCheckConstraint("contact_conversation_channel_check", "channel IN ('chat', 'phone')");
        });

        builder.HasKey(c => c.ConversationId)
               .HasName(PrimaryKeyName);

        builder.Property(c => c.ConversationId)
               .HasColumnName("conversation_id");

        builder.Property(c => c.ContactId)
               .HasColumnName("contact_id");

        builder.Property(c => c.Channel)
               .HasColumnName("channel")
               .HasConversion(
                    channel => channel.ToString().ToLowerInvariant(),
                    text => Enum.Parse<ContactChannel>(text, true));

        builder.Property(c => c.StartedAt)
               .HasColumnName("started_at")
               .HasDefaultValueSql("now()");

        builder.Property(c => c.NotedThrough)
               .HasColumnName("noted_through");

        builder.HasIndex(c => new { c.ContactId, c.StartedAt })
               .HasDatabaseName(ContactStartedIndex)
               .IsDescending(false, true);

        builder.HasOne<Contact>()
               .WithMany()
               .HasForeignKey(c => c.ContactId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ConversationStub>()
               .WithMany()
               .HasForeignKey(c => c.ConversationId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
