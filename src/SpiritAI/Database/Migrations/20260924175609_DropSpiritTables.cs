using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropSpiritTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chatwoot_link",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "contact_conversation",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "contact_identity",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "handoff",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "presence",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "contact",
                schema: "spirit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spirit");

            migrationBuilder.CreateTable(
                name: "chatwoot_link",
                schema: "spirit",
                columns: table => new
                {
                    conversation_id = table.Column<string>(type: "text", nullable: false),
                    announced_handoff_id = table.Column<long>(type: "bigint", nullable: true),
                    chatwoot_conversation_id = table.Column<int>(type: "integer", nullable: false),
                    copied_through = table.Column<int>(type: "integer", nullable: false),
                    noted_phone = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chatwoot_link", x => x.conversation_id);
                    table.ForeignKey(
                        name: "FK_chatwoot_link_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "agentcore",
                        principalTable: "conversation",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact",
                schema: "spirit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    chatwoot_contact_id = table.Column<int>(type: "integer", nullable: true),
                    chatwoot_source_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    display_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "handoff",
                schema: "spirit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    asked_by = table.Column<string>(type: "text", nullable: false),
                    assignee_key = table.Column<string>(type: "text", nullable: true),
                    assignee_name = table.Column<string>(type: "text", nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    conversation_id = table.Column<string>(type: "text", nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    product = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    serial = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tried = table.Column<string>(type: "text", nullable: true),
                    wants = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handoff", x => x.id);
                    table.CheckConstraint("handoff_asked_by_check", "asked_by IN ('bot', 'visitor')");
                    table.CheckConstraint("handoff_status_check", "status IN ('waiting', 'human', 'done')");
                    table.ForeignKey(
                        name: "FK_handoff_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "agentcore",
                        principalTable: "conversation",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "presence",
                schema: "spirit",
                columns: table => new
                {
                    connection_id = table.Column<string>(type: "text", nullable: false),
                    caller_key = table.Column<string>(type: "text", nullable: false),
                    caller_name = table.Column<string>(type: "text", nullable: true),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    kind = table.Column<string>(type: "text", nullable: false),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presence", x => x.connection_id);
                });

            migrationBuilder.CreateTable(
                name: "contact_conversation",
                schema: "spirit",
                columns: table => new
                {
                    conversation_id = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    contact_id = table.Column<long>(type: "bigint", nullable: false),
                    noted_through = table.Column<int>(type: "integer", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_conversation", x => x.conversation_id);
                    table.CheckConstraint("contact_conversation_channel_check", "channel IN ('chat', 'phone')");
                    table.ForeignKey(
                        name: "FK_contact_conversation_contact_contact_id",
                        column: x => x.contact_id,
                        principalSchema: "spirit",
                        principalTable: "contact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contact_conversation_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "agentcore",
                        principalTable: "conversation",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_identity",
                schema: "spirit",
                columns: table => new
                {
                    kind = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    contact_id = table.Column<long>(type: "bigint", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    verified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_identity", x => new { x.kind, x.value });
                    table.CheckConstraint("contact_identity_kind_check", "kind IN ('visitor', 'phone', 'email')");
                    table.ForeignKey(
                        name: "FK_contact_identity_contact_contact_id",
                        column: x => x.contact_id,
                        principalSchema: "spirit",
                        principalTable: "contact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "contact_conversation_contact_id_started_at",
                schema: "spirit",
                table: "contact_conversation",
                columns: new[] { "contact_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "contact_identity_contact_id",
                schema: "spirit",
                table: "contact_identity",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "handoff_open_per_conversation",
                schema: "spirit",
                table: "handoff",
                column: "conversation_id",
                unique: true,
                filter: "status <> 'done'");

            migrationBuilder.CreateIndex(
                name: "handoff_queue",
                schema: "spirit",
                table: "handoff",
                columns: new[] { "status", "asked_at" });

            migrationBuilder.CreateIndex(
                name: "presence_kind_seen_at",
                schema: "spirit",
                table: "presence",
                columns: new[] { "kind", "seen_at" });
        }
    }
}
