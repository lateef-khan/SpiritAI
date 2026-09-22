using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <summary>
    /// Step 2 of the contact and identity design, section 10: gives every widget conversation
    /// made before step 1 shipped a <c>spirit.contact_conversation</c> row, so ownership reads
    /// that already switched to it keep working for conversations older than the switch.
    /// </summary>
    public partial class BackfillContactConversations : Migration
    {
        /// <summary>
        /// The backfill itself, run from <see cref="Up"/> and, unchanged, from the test that proves
        /// it idempotent: running it twice against the same rows leaves one contact, one identity,
        /// and one <c>contact_conversation</c> row per visitor key, however many conversations that
        /// key opened.
        /// </summary>
        /// <remarks>
        /// <c>agentcore.conversation.custom</c> holds <c>ThreadEnvelope</c> as camelCase JSON, so a
        /// widget conversation's owner reads back as <c>custom -&gt;&gt; 'owner'</c>, e.g.
        /// <c>'visitor:abc123'</c>. Every such conversation gets a contact, a verified
        /// <c>'visitor'</c> identity for its key (reusing one step 1 already made for that key, if
        /// any turn against it ran before this migration), and one <c>contact_conversation</c> row.
        /// Procedural rather than set-based so the new identity's <c>contact_id</c> is never guessed
        /// at: a scalar <c>SELECT ... INTO</c> cannot be fooled by two rows racing for the same key
        /// the way a join on row order could.
        /// </remarks>
        public const string BackfillSql = """
            DO $$
            DECLARE
                visitor_key text;
                owning_contact_id bigint;
            BEGIN
                FOR visitor_key IN
                    SELECT DISTINCT substr(c.custom ->> 'owner', length('visitor:') + 1)
                    FROM agentcore.conversation c
                    WHERE c.custom ->> 'owner' LIKE 'visitor:%'
                LOOP
                    SELECT contact_id INTO owning_contact_id
                    FROM spirit.contact_identity
                    WHERE kind = 'visitor' AND value = visitor_key;

                    IF owning_contact_id IS NULL THEN
                        INSERT INTO spirit.contact (created_at)
                        VALUES (now())
                        RETURNING id INTO owning_contact_id;

                        INSERT INTO spirit.contact_identity (kind, value, contact_id, verified, first_seen_at)
                        VALUES ('visitor', visitor_key, owning_contact_id, true, now());
                    END IF;

                    INSERT INTO spirit.contact_conversation (conversation_id, contact_id, channel, started_at)
                    SELECT c.conversation_id, owning_contact_id, 'chat', c.created_at
                    FROM agentcore.conversation c
                    WHERE c.custom ->> 'owner' = 'visitor:' || visitor_key
                    ON CONFLICT (conversation_id) DO NOTHING;
                END LOOP;
            END;
            $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(BackfillSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A documented no-op. Once this migration has run, a contact, a contact_identity, or a
            // contact_conversation row it inserted looks exactly like one the live resolver and
            // ForVisitorAsync/VisitorChatMiddleware wrote for the same key on an ordinary turn that
            // ran after deploy; nothing on any of the three rows marks which wrote it. Deleting by
            // the same predicate the Up migration used would also delete rows real traffic made
            // since, which is not what a rollback of a backfill should do. Reversing this step means
            // restoring a backup taken before it ran.
        }
    }
}
