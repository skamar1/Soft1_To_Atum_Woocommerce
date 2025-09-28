using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soft1_To_Atum.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAtumQuantityField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 28, 19, 20, 51, 125, DateTimeKind.Utc).AddTicks(3880), new DateTime(2025, 9, 28, 19, 20, 51, 125, DateTimeKind.Utc).AddTicks(4010) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2025, 9, 28, 16, 2, 10, 327, DateTimeKind.Utc).AddTicks(3000), new DateTime(2025, 9, 28, 16, 2, 10, 327, DateTimeKind.Utc).AddTicks(3120) });
        }
    }
}
