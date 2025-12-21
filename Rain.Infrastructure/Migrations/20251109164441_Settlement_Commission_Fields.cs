using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Settlement_Commission_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "SupplierOffers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "OrderItems",
                type: "decimal(5,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetToSupplier",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "SupplierOffers");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "NetToSupplier",
                table: "OrderItems");
        }
    }
}
