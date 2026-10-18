using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HornViet.Infrastructure.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SoDiaChiHaiCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "UserAddresses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "UserAddresses",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WardCode",
                table: "UserAddresses",
                type: "character varying(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "UserAddresses");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "UserAddresses");

            migrationBuilder.DropColumn(
                name: "WardCode",
                table: "UserAddresses");
        }
    }
}
