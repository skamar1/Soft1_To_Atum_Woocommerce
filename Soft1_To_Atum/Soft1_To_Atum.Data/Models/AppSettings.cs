using System.ComponentModel.DataAnnotations;

namespace Soft1_To_Atum.Data.Models;

/// <summary>
/// Global application settings shared across all stores
/// </summary>
public class AppSettings
{
    [Key]
    public int Id { get; set; } = 1; // Single row table - always ID = 1

    // WooCommerce Settings (shared across all stores)
    public string WooCommerceUrl { get; set; } = string.Empty;
    public string WooCommerceConsumerKey { get; set; } = string.Empty;
    public string WooCommerceConsumerSecret { get; set; } = string.Empty;
    public string WooCommerceVersion { get; set; } = "wc/v3";

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

    public void UpdateFromApiModel(ApiSettingsModel apiModel)
    {
        // WooCommerce Settings
        WooCommerceUrl = apiModel.WooCommerce.Url;
        WooCommerceConsumerKey = apiModel.WooCommerce.ConsumerKey;
        WooCommerceConsumerSecret = apiModel.WooCommerce.ConsumerSecret;
        WooCommerceVersion = apiModel.WooCommerce.Version;

        // Email Settings
        EmailSmtpHost = apiModel.Email.SmtpHost;
        EmailSmtpPort = apiModel.Email.SmtpPort;
        EmailUsername = apiModel.Email.Username;
        EmailPassword = apiModel.Email.Password;
        EmailFromEmail = apiModel.Email.FromEmail;
        EmailToEmail = apiModel.Email.ToEmail;

        // Sync Settings
        SyncIntervalMinutes = apiModel.Sync.IntervalMinutes;
        SyncAutoSync = apiModel.Sync.AutoSync;
        SyncEmailNotifications = apiModel.Sync.EmailNotifications;

        // Matching Settings
        MatchingPrimaryField = apiModel.Matching.PrimaryField;
        MatchingSecondaryField = apiModel.Matching.SecondaryField;
        MatchingCreateMissingProducts = apiModel.Matching.CreateMissingProducts;
        MatchingUpdateExistingProducts = apiModel.Matching.UpdateExistingProducts;

        // Field Mapping Settings
        FieldMappingSku = apiModel.FieldMapping.Sku;
        FieldMappingName = apiModel.FieldMapping.Name;
        FieldMappingPrice = apiModel.FieldMapping.Price;
        FieldMappingStockQuantity = apiModel.FieldMapping.StockQuantity;
        FieldMappingCategory = apiModel.FieldMapping.Category;
        FieldMappingUnit = apiModel.FieldMapping.Unit;
        FieldMappingVat = apiModel.FieldMapping.Vat;

        UpdatedAt = DateTime.UtcNow;
    }

    public ApiSettingsModel ToApiModel()
    {
        return new ApiSettingsModel
        {
            WooCommerce = new WooCommerceSettings
            {
                Url = WooCommerceUrl,
                ConsumerKey = WooCommerceConsumerKey,
                ConsumerSecret = WooCommerceConsumerSecret,
                Version = WooCommerceVersion
            },
            Email = new EmailSettings
            {
                SmtpHost = EmailSmtpHost,
                SmtpPort = EmailSmtpPort,
                Username = EmailUsername,
                Password = EmailPassword,
                FromEmail = EmailFromEmail,
                ToEmail = EmailToEmail
            },
            Sync = new SyncSettings
            {
                IntervalMinutes = SyncIntervalMinutes,
                AutoSync = SyncAutoSync,
                EmailNotifications = SyncEmailNotifications
            },
            Matching = new MatchingSettings
            {
                PrimaryField = MatchingPrimaryField,
                SecondaryField = MatchingSecondaryField,
                CreateMissingProducts = MatchingCreateMissingProducts,
                UpdateExistingProducts = MatchingUpdateExistingProducts
            },
            FieldMapping = new FieldMappingSettings
            {
                Sku = FieldMappingSku,
                Name = FieldMappingName,
                Price = FieldMappingPrice,
                StockQuantity = FieldMappingStockQuantity,
                Category = FieldMappingCategory,
                Unit = FieldMappingUnit,
                Vat = FieldMappingVat
            }
        };
    }
}