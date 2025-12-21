using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Product_SellerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerName",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerName",
                table: "Products");
        }
    }
}
