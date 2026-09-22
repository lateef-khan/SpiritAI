using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContactMergedInto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contact_contact_merged_into",
                schema: "spirit",
                table: "contact");

            migrationBuilder.DropIndex(
                name: "IX_contact_merged_into",
                schema: "spirit",
                table: "contact");

            migrationBuilder.DropColumn(
                name: "merged_into",
                schema: "spirit",
                table: "contact");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "merged_into",
                schema: "spirit",
                table: "contact",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_merged_into",
                schema: "spirit",
                table: "contact",
                column: "merged_into");

            migrationBuilder.AddForeignKey(
                name: "FK_contact_contact_merged_into",
                schema: "spirit",
                table: "contact",
                column: "merged_into",
                principalSchema: "spirit",
                principalTable: "contact",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
