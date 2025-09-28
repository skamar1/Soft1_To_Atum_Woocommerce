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

    public void UpdateFromApiModel(ApiSettingsModel apiModel)
    {
        StoreName = apiModel.Name;
        StoreEnabled = apiModel.Enabled;

        // SoftOne Go Settings
        SoftOneGoBaseUrl = apiModel.SoftOneGo.BaseUrl;
        SoftOneGoAppId = apiModel.SoftOneGo.AppId;
        SoftOneGoToken = apiModel.SoftOneGo.Token;
        SoftOneGoS1Code = apiModel.SoftOneGo.S1Code;
        SoftOneGoFilters = apiModel.SoftOneGo.Filters;

        // WooCommerce Settings
        WooCommerceUrl = apiModel.WooCommerce.Url;
        WooCommerceConsumerKey = apiModel.WooCommerce.ConsumerKey;
        WooCommerceConsumerSecret = apiModel.WooCommerce.ConsumerSecret;
        WooCommerceVersion = apiModel.WooCommerce.Version;

        // ATUM Settings
        AtumLocationId = apiModel.ATUM.LocationId;
        AtumLocationName = apiModel.ATUM.LocationName;

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
            Name = StoreName,
            Enabled = StoreEnabled,
            SoftOneGo = new SoftOneGoSettings
            {
                BaseUrl = SoftOneGoBaseUrl,
                AppId = SoftOneGoAppId,
                Token = SoftOneGoToken,
                S1Code = SoftOneGoS1Code,
                Filters = SoftOneGoFilters
            },
            WooCommerce = new WooCommerceSettings
            {
                Url = WooCommerceUrl,
                ConsumerKey = WooCommerceConsumerKey,
                ConsumerSecret = WooCommerceConsumerSecret,
                Version = WooCommerceVersion
            },
            ATUM = new AtumSettings
            {
                LocationId = AtumLocationId,
                LocationName = AtumLocationName
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