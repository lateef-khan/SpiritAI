using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class Presence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_presence",
                schema: "spirit");

            migrationBuilder.DropColumn(
                name: "visitor_seen_at",
                schema: "spirit",
                table: "handoff");

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
                name: "presence_kind_seen_at",
                schema: "spirit",
                table: "presence",
                columns: new[] { "kind", "seen_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "presence",
                schema: "spirit");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "visitor_seen_at",
                schema: "spirit",
                table: "handoff",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff_presence",
                schema: "spirit",
                columns: table => new
                {
                    connection_id = table.Column<string>(type: "text", nullable: false),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    staff_key = table.Column<string>(type: "text", nullable: false),
                    staff_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_presence", x => x.connection_id);
                });
        }
    }
}
