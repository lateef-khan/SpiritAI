using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class HandoffPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phone",
                schema: "spirit",
                table: "handoff",
                type: "text",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "email",
                schema: "spirit",
                table: "handoff");

            migrationBuilder.RenameColumn(
                name: "noted_email",
                schema: "spirit",
                table: "chatwoot_link",
                newName: "noted_phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "spirit",
                table: "handoff",
                type: "text",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "phone",
                schema: "spirit",
                table: "handoff");

            migrationBuilder.RenameColumn(
                name: "noted_phone",
                schema: "spirit",
                table: "chatwoot_link",
                newName: "noted_email");
        }
    }
}
