using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Customers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Bills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Bills",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DiscountBp = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerId",
                table: "Bills",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PublicId",
                table: "Customers",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Customers_CustomerId",
                table: "Bills",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Customers_CustomerId",
                table: "Bills");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CustomerId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Bills");
        }
    }
}
