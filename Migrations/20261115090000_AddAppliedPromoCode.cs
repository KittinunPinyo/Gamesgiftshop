using Gamesgiftshop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamesgiftshop.Migrations
{
    [DbContext(typeof(Csi402dbContext))]
    [Migration("20261115090000_AddAppliedPromoCode")]
    public partial class AddAppliedPromoCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliedPromoCode",
                table: "orders",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId_AppliedPromoCode",
                table: "orders",
                columns: new[] { "UserId", "AppliedPromoCode" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_UserId_AppliedPromoCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "AppliedPromoCode",
                table: "orders");
        }
    }
}
