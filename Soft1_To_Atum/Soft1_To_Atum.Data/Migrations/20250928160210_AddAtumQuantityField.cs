using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soft1_To_Atum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAtumQuantityField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AtumQuantity",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 28, 16, 2, 10, 327, DateTimeKind.Utc).AddTicks(3000), new DateTime(2025, 9, 28, 16, 2, 10, 327, DateTimeKind.Utc).AddTicks(3120) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtumQuantity",
                table: "Products");

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 28, 14, 14, 59, 134, DateTimeKind.Utc).AddTicks(3070), new DateTime(2025, 9, 28, 14, 14, 59, 134, DateTimeKind.Utc).AddTicks(3210) });
        }
    }
}
