using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HornViet.Infrastructure.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ThemGiaoHangNhanhVaoDonHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GhnExpectedDeliveryTime",
                table: "Orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhnOrderCode",
                table: "Orders",
                type: "character varying(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhnStatus",
                table: "Orders",
                type: "character varying(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GhnStatusTime",
                table: "Orders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "Orders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ToDistrictId",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToWardCode",
                table: "Orders",
                type: "character varying(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_GhnOrderCode",
                table: "Orders",
                column: "GhnOrderCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_GhnOrderCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GhnExpectedDeliveryTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GhnOrderCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GhnStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "GhnStatusTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ToDistrictId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ToWardCode",
                table: "Orders");
        }
    }
}
