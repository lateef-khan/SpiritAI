using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class Handoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spirit");

            migrationBuilder.CreateTable(
                name: "handoff",
                schema: "spirit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    call_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    asked_by = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    assignee_key = table.Column<string>(type: "text", nullable: true),
                    assignee_name = table.Column<string>(type: "text", nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_handoff", x => x.id);
                    table.CheckConstraint("handoff_asked_by_check", "asked_by IN ('bot', 'visitor')");
                    table.CheckConstraint("handoff_status_check", "status IN ('waiting', 'human', 'done')");
                    table.ForeignKey(
                        name: "FK_handoff_call_call_id",
                        column: x => x.call_id,
                        principalSchema: "agentcore",
                        principalTable: "call",
                        principalColumn: "call_id",
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
                    kind = table.Column<string>(type: "text", nullable: false),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presence", x => x.connection_id);
                });

            migrationBuilder.CreateIndex(
                name: "handoff_open_per_call",
                schema: "spirit",
                table: "handoff",
                column: "call_id",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handoff",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "presence",
                schema: "spirit");
        }
    }
}
