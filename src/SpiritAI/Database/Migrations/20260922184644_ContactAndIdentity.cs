using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class ContactAndIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contact",
                schema: "spirit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    merged_into = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact", x => x.id);
                    table.ForeignKey(
                        name: "FK_contact_contact_merged_into",
                        column: x => x.merged_into,
                        principalSchema: "spirit",
                        principalTable: "contact",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contact_conversation",
                schema: "spirit",
                columns: table => new
                {
                    conversation_id = table.Column<string>(type: "text", nullable: false),
                    contact_id = table.Column<long>(type: "bigint", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    noted_through = table.Column<int>(type: "integer", nullable: true)
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
                    verified = table.Column<bool>(type: "boolean", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                name: "IX_contact_merged_into",
                schema: "spirit",
                table: "contact",
                column: "merged_into");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_conversation",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "contact_identity",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "contact",
                schema: "spirit");
        }
    }
}
