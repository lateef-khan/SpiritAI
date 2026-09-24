using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class HandoffSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "product",
                schema: "spirit",
                table: "handoff",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serial",
                schema: "spirit",
                table: "handoff",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tried",
                schema: "spirit",
                table: "handoff",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wants",
                schema: "spirit",
                table: "handoff",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "product",
                schema: "spirit",
                table: "handoff");

            migrationBuilder.DropColumn(
                name: "serial",
                schema: "spirit",
                table: "handoff");

            migrationBuilder.DropColumn(
                name: "tried",
                schema: "spirit",
                table: "handoff");

            migrationBuilder.DropColumn(
                name: "wants",
                schema: "spirit",
                table: "handoff");
        }
    }
}
