using System.ComponentModel.DataAnnotations;

namespace Soft1_To_Atum.Data.Models;

public class AppSettings
{
    [Key]
    public int Id { get; set; } = 1; // Single row table - always ID = 1

    // Store Information
    public string StoreName { get; set; } = "Κατάστημα Κέντρο";
    public bool StoreEnabled { get; set; } = true;

    // SoftOne Go Settings
    public string SoftOneGoBaseUrl { get; set; } = "https://go.s1cloud.net/s1services";
    public string SoftOneGoAppId { get; set; } = "703";
    public string SoftOneGoToken { get; set; } = string.Empty;
    public string SoftOneGoS1Code { get; set; } = string.Empty;
    public string SoftOneGoFilters { get; set; } = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999";

    // WooCommerce Settings
    public string WooCommerceUrl { get; set; } = string.Empty;
    public string WooCommerceConsumerKey { get; set; } = string.Empty;
    public string WooCommerceConsumerSecret { get; set; } = string.Empty;
    public string WooCommerceVersion { get; set; } = "wc/v3";

    // ATUM Settings
    public int AtumLocationId { get; set; } = 870;
    public string AtumLocationName { get; set; } = "store1_location";

    // Email Settings
    public string EmailSmtpHost { get; set; } = string.Empty;
    public int EmailSmtpPort { get; set; } = 587;
    public string EmailUsername { get; set; } = string.Empty;
    public string EmailPassword { get; set; } = string.Empty;
    public string EmailFromEmail { get; set; } = string.Empty;
    public string EmailToEmail { get; set; } = string.Empty;

    // Sync Settings
    public int SyncIntervalMinutes { get; set; } = 15;
    public bool SyncAutoSync { get; set; } = true;
    public bool SyncEmailNotifications { get; set; } = true;

    // Matching Settings
    public string MatchingPrimaryField { get; set; } = "sku";
    public string MatchingSecondaryField { get; set; } = "barcode";
    public bool MatchingCreateMissingProducts { get; set; } = true;
    public bool MatchingUpdateExistingProducts { get; set; } = true;

    // Field Mapping Settings
    public string FieldMappingSku { get; set; } = "ITEM.CODE1";
    public string FieldMappingName { get; set; } = "ITEM.NAME";
    public string FieldMappingPrice { get; set; } = "ITEM.PRICER";
    public string FieldMappingStockQuantity { get; set; } = "ITEM.MTRL_ITEMTRDATA_QTY1";
    public string FieldMappingCategory { get; set; } = "ITEM.MTRCATEGORY";
    public string FieldMappingUnit { get; set; } = "ITEM.MTRUNIT1";
    public string FieldMappingVat { get; set; } = "ITEM.VAT";

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}