using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.ApiService.Extensions;

public static class SettingsExtensions
{
    // Extension methods for AppSettings (global settings)
    public static ApiSettingsModel ToApiModel(this AppSettings appSettings)
    {
        return new ApiSettingsModel
        {
            WooCommerce = new WooCommerceSettings
            {
                Url = appSettings.WooCommerceUrl,
                ConsumerKey = appSettings.WooCommerceConsumerKey,
                ConsumerSecret = appSettings.WooCommerceConsumerSecret,
                Version = appSettings.WooCommerceVersion
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

    // Extension methods for StoreSettings (per-store settings)
    public static StoreSettingsApiModel ToApiModel(this StoreSettings storeSettings)
    {
        return new StoreSettingsApiModel
        {
            Id = storeSettings.Id,
            Name = storeSettings.StoreName,
            Enabled = storeSettings.StoreEnabled,
            SoftOneGo = new SoftOneGoSettings
            {
                BaseUrl = storeSettings.SoftOneGoBaseUrl,
                AppId = storeSettings.SoftOneGoAppId,
                Token = storeSettings.SoftOneGoToken,
                S1Code = storeSettings.SoftOneGoS1Code,
                Filters = storeSettings.SoftOneGoFilters
            },
            ATUM = new AtumSettings
            {
                LocationId = storeSettings.AtumLocationId,
                LocationName = storeSettings.AtumLocationName
            }
        };
    }

    public static void UpdateFromApiModel(this StoreSettings storeSettings, StoreSettingsApiModel apiModel)
    {
        storeSettings.StoreName = apiModel.Name;
        storeSettings.StoreEnabled = apiModel.Enabled;

        // SoftOne Go
        storeSettings.SoftOneGoBaseUrl = apiModel.SoftOneGo.BaseUrl;
        storeSettings.SoftOneGoAppId = apiModel.SoftOneGo.AppId;
        storeSettings.SoftOneGoToken = apiModel.SoftOneGo.Token;
        storeSettings.SoftOneGoS1Code = apiModel.SoftOneGo.S1Code;
        storeSettings.SoftOneGoFilters = apiModel.SoftOneGo.Filters;

        // ATUM
        storeSettings.AtumLocationId = apiModel.ATUM.LocationId;
        storeSettings.AtumLocationName = apiModel.ATUM.LocationName;

        storeSettings.UpdatedAt = DateTime.UtcNow;
    }
}