using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MenuItemTaxAndVariantStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GstRateBp",
                table: "MenuItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ItemVariants",
                type: "INTEGER",
                nullable: false,
                defaultValue: true); // existing variants stay on the menu
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GstRateBp",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ItemVariants");
        }
    }
}
