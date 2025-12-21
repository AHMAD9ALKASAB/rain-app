using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rain.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_SupplierApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyOrShopName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneWithCountry = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductScope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResidenceLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExactLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierApplications", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierApplications");
        }
    }
}
