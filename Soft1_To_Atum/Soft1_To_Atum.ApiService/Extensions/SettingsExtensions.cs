using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.ApiService.Extensions;

public static class SettingsExtensions
{
    public static ApiSettingsModel ToApiModel(this AppSettings appSettings)
    {
        return new ApiSettingsModel
        {
            Name = appSettings.StoreName,
            Enabled = appSettings.StoreEnabled,
            SoftOneGo = new SoftOneGoSettings
            {
                BaseUrl = appSettings.SoftOneGoBaseUrl,
                AppId = appSettings.SoftOneGoAppId,
                Token = appSettings.SoftOneGoToken,
                S1Code = appSettings.SoftOneGoS1Code,
                Filters = appSettings.SoftOneGoFilters
            },
            WooCommerce = new WooCommerceSettings
            {
                Url = appSettings.WooCommerceUrl,
                ConsumerKey = appSettings.WooCommerceConsumerKey,
                ConsumerSecret = appSettings.WooCommerceConsumerSecret,
                Version = appSettings.WooCommerceVersion
            },
            ATUM = new AtumSettings
            {
                LocationId = appSettings.AtumLocationId,
                LocationName = appSettings.AtumLocationName
            },
            Email = new EmailSettings
            {
                SmtpHost = appSettings.EmailSmtpHost,
                SmtpPort = appSettings.EmailSmtpPort,
                Username = appSettings.EmailUsername,
                Password = appSettings.EmailPassword,
                FromEmail = appSettings.EmailFromEmail,
                ToEmail = appSettings.EmailToEmail
            },
            Sync = new SyncSettings
            {
                IntervalMinutes = appSettings.SyncIntervalMinutes,
                AutoSync = appSettings.SyncAutoSync,
                EmailNotifications = appSettings.SyncEmailNotifications
            },
            Matching = new MatchingSettings
            {
                PrimaryField = appSettings.MatchingPrimaryField,
                SecondaryField = appSettings.MatchingSecondaryField,
                CreateMissingProducts = appSettings.MatchingCreateMissingProducts,
                UpdateExistingProducts = appSettings.MatchingUpdateExistingProducts
            },
            FieldMapping = new FieldMappingSettings
            {
                Sku = appSettings.FieldMappingSku,
                Name = appSettings.FieldMappingName,
                Price = appSettings.FieldMappingPrice,
                StockQuantity = appSettings.FieldMappingStockQuantity,
                Category = appSettings.FieldMappingCategory,
                Unit = appSettings.FieldMappingUnit,
                Vat = appSettings.FieldMappingVat
            }
        };
    }

    public static void UpdateFromApiModel(this AppSettings appSettings, ApiSettingsModel apiModel)
    {
        appSettings.StoreName = apiModel.Name;
        appSettings.StoreEnabled = apiModel.Enabled;

        // SoftOne Go
        appSettings.SoftOneGoBaseUrl = apiModel.SoftOneGo.BaseUrl;
        appSettings.SoftOneGoAppId = apiModel.SoftOneGo.AppId;
        appSettings.SoftOneGoToken = apiModel.SoftOneGo.Token;
        appSettings.SoftOneGoS1Code = apiModel.SoftOneGo.S1Code;
        appSettings.SoftOneGoFilters = apiModel.SoftOneGo.Filters;

        // WooCommerce
        appSettings.WooCommerceUrl = apiModel.WooCommerce.Url;
        appSettings.WooCommerceConsumerKey = apiModel.WooCommerce.ConsumerKey;
        appSettings.WooCommerceConsumerSecret = apiModel.WooCommerce.ConsumerSecret;
        appSettings.WooCommerceVersion = apiModel.WooCommerce.Version;

        // ATUM
        appSettings.AtumLocationId = apiModel.ATUM.LocationId;
        appSettings.AtumLocationName = apiModel.ATUM.LocationName;

        // Email
        appSettings.EmailSmtpHost = apiModel.Email.SmtpHost;
        appSettings.EmailSmtpPort = apiModel.Email.SmtpPort;
        appSettings.EmailUsername = apiModel.Email.Username;
        appSettings.EmailPassword = apiModel.Email.Password;
        appSettings.EmailFromEmail = apiModel.Email.FromEmail;
        appSettings.EmailToEmail = apiModel.Email.ToEmail;

        // Sync
        appSettings.SyncIntervalMinutes = apiModel.Sync.IntervalMinutes;
        appSettings.SyncAutoSync = apiModel.Sync.AutoSync;
        appSettings.SyncEmailNotifications = apiModel.Sync.EmailNotifications;

        // Matching
        appSettings.MatchingPrimaryField = apiModel.Matching.PrimaryField;
        appSettings.MatchingSecondaryField = apiModel.Matching.SecondaryField;
        appSettings.MatchingCreateMissingProducts = apiModel.Matching.CreateMissingProducts;
        appSettings.MatchingUpdateExistingProducts = apiModel.Matching.UpdateExistingProducts;

        // Field Mapping
        appSettings.FieldMappingSku = apiModel.FieldMapping.Sku;
        appSettings.FieldMappingName = apiModel.FieldMapping.Name;
        appSettings.FieldMappingPrice = apiModel.FieldMapping.Price;
        appSettings.FieldMappingStockQuantity = apiModel.FieldMapping.StockQuantity;
        appSettings.FieldMappingCategory = apiModel.FieldMapping.Category;
        appSettings.FieldMappingUnit = apiModel.FieldMapping.Unit;
        appSettings.FieldMappingVat = apiModel.FieldMapping.Vat;

        appSettings.UpdatedAt = DateTime.UtcNow;
    }
}