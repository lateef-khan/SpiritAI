using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConversationRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversation_read",
                schema: "spirit",
                columns: table => new
                {
                    conversation_id = table.Column<string>(type: "text", nullable: false),
                    staff_key = table.Column<string>(type: "text", nullable: false),
                    seen_ordinal = table.Column<int>(type: "integer", nullable: false),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_read", x => new { x.conversation_id, x.staff_key });
                    table.ForeignKey(
                        name: "FK_conversation_read_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "agentcore",
                        principalTable: "conversation",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_read",
                schema: "spirit");
        }
    }
}
