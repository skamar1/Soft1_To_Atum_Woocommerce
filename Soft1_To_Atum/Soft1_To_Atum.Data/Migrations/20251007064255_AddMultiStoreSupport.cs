using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soft1_To_Atum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiStoreSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SoftOneId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AtumLocationId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "AtumLocationName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SoftOneGoAppId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SoftOneGoBaseUrl",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SoftOneGoFilters",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SoftOneGoS1Code",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SoftOneGoToken",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "StoreEnabled",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "StoreName",
                table: "AppSettings");

            migrationBuilder.AddColumn<int>(
                name: "StoreSettingsId",
                table: "SyncLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StoreSettingsId",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StoreName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    StoreEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SoftOneGoBaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SoftOneGoAppId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SoftOneGoToken = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SoftOneGoS1Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SoftOneGoFilters = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    AtumLocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtumLocationName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "UpdatedAt" },
                values: new object[] { new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "StoreSettings",
                columns: new[] { "Id", "AtumLocationId", "AtumLocationName", "CreatedAt", "SoftOneGoAppId", "SoftOneGoBaseUrl", "SoftOneGoFilters", "SoftOneGoS1Code", "SoftOneGoToken", "StoreEnabled", "StoreName", "UpdatedAt" },
                values: new object[] { 1, 870, "store1_location", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "703", "https://go.s1cloud.net/s1services", "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999", "", "", true, "Κατάστημα Κέντρο", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_SyncLogs_StoreSettingsId",
                table: "SyncLogs",
                column: "StoreSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_SoftOneId",
                table: "Products",
                column: "SoftOneId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_StoreSettingsId",
                table: "Products",
                column: "StoreSettingsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_StoreSettings_StoreSettingsId",
                table: "Products",
                column: "StoreSettingsId",
                principalTable: "StoreSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SyncLogs_StoreSettings_StoreSettingsId",
                table: "SyncLogs",
                column: "StoreSettingsId",
                principalTable: "StoreSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_StoreSettings_StoreSettingsId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_SyncLogs_StoreSettings_StoreSettingsId",
                table: "SyncLogs");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropIndex(
                name: "IX_SyncLogs_StoreSettingsId",
                table: "SyncLogs");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_SoftOneId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_StoreSettingsId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StoreSettingsId",
                table: "SyncLogs");

            migrationBuilder.DropColumn(
                name: "StoreSettingsId",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "AtumLocationId",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AtumLocationName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoftOneGoAppId",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoftOneGoBaseUrl",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoftOneGoFilters",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoftOneGoS1Code",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SoftOneGoToken",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "StoreEnabled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AppSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AtumLocationId", "AtumLocationName", "CreatedAt", "SoftOneGoAppId", "SoftOneGoBaseUrl", "SoftOneGoFilters", "SoftOneGoS1Code", "SoftOneGoToken", "StoreEnabled", "StoreName", "UpdatedAt" },
                values: new object[] { 870, "store1_location", new DateTime(2025, 9, 28, 19, 21, 49, 776, DateTimeKind.Utc).AddTicks(7940), "703", "https://go.s1cloud.net/s1services", "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999", "", "", true, "Κατάστημα Κέντρο", new DateTime(2025, 9, 28, 19, 21, 49, 776, DateTimeKind.Utc).AddTicks(8060) });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SoftOneId",
                table: "Products",
                column: "SoftOneId",
                unique: true);
        }
    }
}
