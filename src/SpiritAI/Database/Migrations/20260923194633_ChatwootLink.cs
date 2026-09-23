using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChatwootLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chatwoot_link",
                schema: "spirit",
                columns: table => new
                {
                    conversation_id = table.Column<string>(type: "text", nullable: false),
                    chatwoot_conversation_id = table.Column<int>(type: "integer", nullable: false),
                    copied_through = table.Column<int>(type: "integer", nullable: false),
                    announced_handoff_id = table.Column<long>(type: "bigint", nullable: true),
                    noted_email = table.Column<string>(type: "text", nullable: true),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chatwoot_link",
                schema: "spirit");
        }
    }
}
