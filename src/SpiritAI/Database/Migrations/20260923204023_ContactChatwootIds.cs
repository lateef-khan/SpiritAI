using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class ContactChatwootIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "chatwoot_contact_id",
                schema: "spirit",
                table: "contact",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chatwoot_source_id",
                schema: "spirit",
                table: "contact",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chatwoot_contact_id",
                schema: "spirit",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "chatwoot_source_id",
                schema: "spirit",
                table: "contact");
        }
    }
}
