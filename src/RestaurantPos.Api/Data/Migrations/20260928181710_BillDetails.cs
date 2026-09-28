using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BillDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                table: "Bills",
                type: "TEXT",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<long>(
                name: "DiscountValue",
                table: "Bills",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LastPrintedAt",
                table: "Bills",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrintCount",
                table: "Bills",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaxMode",
                table: "Bills",
                type: "TEXT",
                nullable: false,
                defaultValue: "Regular");

            migrationBuilder.AddColumn<int>(
                name: "GstRateBp",
                table: "BillLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 500); // the default rate before per-line rates existed

            migrationBuilder.AddColumn<long>(
                name: "RemovedAt",
                table: "BillLines",
                type: "INTEGER",
                nullable: true);

            // Bills made before this change stored only the discount amount.
            migrationBuilder.Sql("UPDATE \"Bills\" SET \"DiscountKind\" = 'Amount', \"DiscountValue\" = \"DiscountPaise\" WHERE \"DiscountPaise\" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountKind",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "LastPrintedAt",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PrintCount",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TaxMode",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "GstRateBp",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "BillLines");
        }
    }
}
