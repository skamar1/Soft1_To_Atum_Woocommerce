using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soft1_To_Atum.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithAllFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
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
                    WooCommerceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    WooCommerceConsumerKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    WooCommerceConsumerSecret = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    WooCommerceVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AtumLocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtumLocationName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EmailSmtpHost = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EmailSmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    EmailUsername = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EmailPassword = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EmailFromEmail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EmailToEmail = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncAutoSync = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncEmailNotifications = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchingPrimaryField = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MatchingSecondaryField = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MatchingCreateMissingProducts = table.Column<bool>(type: "INTEGER", nullable: false),
                    MatchingUpdateExistingProducts = table.Column<bool>(type: "INTEGER", nullable: false),
                    FieldMappingSku = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FieldMappingName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FieldMappingPrice = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FieldMappingStockQuantity = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FieldMappingCategory = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FieldMappingUnit = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FieldMappingVat = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SoftOneId = table.Column<string>(type: "TEXT", nullable: false),
                    WooCommerceId = table.Column<string>(type: "TEXT", nullable: false),
                    AtumId = table.Column<string>(type: "TEXT", nullable: false),
                    InternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", nullable: false),
                    Group = table.Column<string>(type: "TEXT", nullable: false),
                    Vat = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WholesalePrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalePrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Discount = table.Column<decimal>(type: "TEXT", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImageData = table.Column<string>(type: "TEXT", nullable: false),
                    ZoomInfo = table.Column<string>(type: "TEXT", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    WooCommerceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    WooCommerceKey = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    WooCommerceSecret = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalProducts = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedProducts = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedProducts = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedProducts = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorDetails = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "AtumLocationId", "AtumLocationName", "CreatedAt", "EmailFromEmail", "EmailPassword", "EmailSmtpHost", "EmailSmtpPort", "EmailToEmail", "EmailUsername", "FieldMappingCategory", "FieldMappingName", "FieldMappingPrice", "FieldMappingSku", "FieldMappingStockQuantity", "FieldMappingUnit", "FieldMappingVat", "MatchingCreateMissingProducts", "MatchingPrimaryField", "MatchingSecondaryField", "MatchingUpdateExistingProducts", "SoftOneGoAppId", "SoftOneGoBaseUrl", "SoftOneGoFilters", "SoftOneGoS1Code", "SoftOneGoToken", "StoreEnabled", "StoreName", "SyncAutoSync", "SyncEmailNotifications", "SyncIntervalMinutes", "UpdatedAt", "WooCommerceConsumerKey", "WooCommerceConsumerSecret", "WooCommerceUrl", "WooCommerceVersion" },
                values: new object[] { 1, 870, "store1_location", new DateTime(2025, 9, 28, 14, 14, 59, 134, DateTimeKind.Utc).AddTicks(3070), "", "", "", 587, "", "", "ITEM.MTRCATEGORY", "ITEM.NAME", "ITEM.PRICER", "ITEM.CODE1", "ITEM.MTRL_ITEMTRDATA_QTY1", "ITEM.MTRUNIT1", "ITEM.VAT", true, "sku", "barcode", true, "703", "https://go.s1cloud.net/s1services", "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999", "", "", true, "Κατάστημα Κέντρο", true, true, 15, new DateTime(2025, 9, 28, 14, 14, 59, 134, DateTimeKind.Utc).AddTicks(3210), "", "", "", "wc/v3" });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "SyncLogs");
        }
    }
}
