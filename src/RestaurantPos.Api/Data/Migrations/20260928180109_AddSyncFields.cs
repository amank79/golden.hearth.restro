using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantPos.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "Payments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Payments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "Payments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "MenuItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "MenuItems",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "MenuItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "ItemVariants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "ItemVariants",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "ItemVariants",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Categories",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "Bills",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Bills",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "Bills",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "BillLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "BillLines",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "UpdatedAt",
                table: "BillLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.UpdateData(
                table: "Settings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "PublicId", "UpdatedAt" },
                values: new object[] { 1309207560192000000L, new Guid("6f1c8c52-3a4e-4b8e-9d0a-5b1f2e7c9a01"), 1309207560192000000L });

            // Rows that already exist get a random PublicId (upper-case text, the way EF stores a Guid in SQLite)
            // before the unique indexes are created. Bills and payments take their existing time as CreatedAt.
            foreach (var table in new[] { "Payments", "MenuItems", "ItemVariants", "Categories", "Bills", "BillLines" })
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" SET \"PublicId\" = hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || " +
                    "substr(hex(randomblob(2)), 2) || '-' || substr('89AB', 1 + (abs(random()) % 4), 1) || " +
                    "substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6)) " +
                    "WHERE \"PublicId\" = '00000000-0000-0000-0000-000000000000';");
            }
            migrationBuilder.Sql("UPDATE \"Bills\" SET \"CreatedAt\" = \"OpenedAt\", \"UpdatedAt\" = \"OpenedAt\" WHERE \"CreatedAt\" = 0;");
            migrationBuilder.Sql("UPDATE \"Payments\" SET \"CreatedAt\" = \"At\", \"UpdatedAt\" = \"At\" WHERE \"CreatedAt\" = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_PublicId",
                table: "Settings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PublicId",
                table: "Payments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_PublicId",
                table: "MenuItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemVariants_PublicId",
                table: "ItemVariants",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_PublicId",
                table: "Categories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PublicId",
                table: "Bills",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_PublicId",
                table: "BillLines",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Settings_PublicId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PublicId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_PublicId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_ItemVariants_PublicId",
                table: "ItemVariants");

            migrationBuilder.DropIndex(
                name: "IX_Categories_PublicId",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Bills_PublicId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_BillLines_PublicId",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ItemVariants");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "ItemVariants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ItemVariants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "BillLines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BillLines");
        }
    }
}
