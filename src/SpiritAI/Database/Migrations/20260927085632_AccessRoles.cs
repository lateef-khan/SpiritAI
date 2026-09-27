using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpiritAI.Database.Migrations
{
    /// <inheritdoc />
    public partial class AccessRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spirit");

            migrationBuilder.CreateTable(
                name: "role",
                schema: "spirit",
                columns: table => new
                {
                    name = table.Column<string>(type: "text", nullable: false),
                    access_group = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.name);
                    table.CheckConstraint("role_access_group_check", "access_group IN ('Guest', 'Dealer', 'TechService', 'InsideSales', 'InsideSalesSupervisor', 'TechServiceManager', 'InsideSalesManager', 'Admin')");
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                schema: "spirit",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role", x => new { x.user_id, x.role });
                    table.ForeignKey(
                        name: "FK_user_role_role_role",
                        column: x => x.role,
                        principalSchema: "spirit",
                        principalTable: "role",
                        principalColumn: "name",
                        onUpdate: ReferentialAction.Cascade,
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_role_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "neon_auth",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_role_role",
                schema: "spirit",
                table: "user_role",
                column: "role");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_role",
                schema: "spirit");

            migrationBuilder.DropTable(
                name: "role",
                schema: "spirit");
        }
    }
}
