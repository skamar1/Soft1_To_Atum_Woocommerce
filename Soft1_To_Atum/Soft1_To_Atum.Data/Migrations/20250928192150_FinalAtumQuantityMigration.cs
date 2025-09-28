using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soft1_To_Atum.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalAtumQuantityMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 28, 19, 21, 49, 776, DateTimeKind.Utc).AddTicks(7940), new DateTime(2025, 9, 28, 19, 21, 49, 776, DateTimeKind.Utc).AddTicks(8060) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 28, 19, 20, 51, 125, DateTimeKind.Utc).AddTicks(3880), new DateTime(2025, 9, 28, 19, 20, 51, 125, DateTimeKind.Utc).AddTicks(4010) });
        }
    }
}
