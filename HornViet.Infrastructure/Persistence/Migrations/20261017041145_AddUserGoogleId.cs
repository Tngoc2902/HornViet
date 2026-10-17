using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HornViet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGoogleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "AbpUsers",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbpUsers_GoogleId",
                table: "AbpUsers",
                column: "GoogleId",
                unique: true,
                filter: "[GoogleId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AbpUsers_GoogleId",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "AbpUsers");
        }
    }
}
