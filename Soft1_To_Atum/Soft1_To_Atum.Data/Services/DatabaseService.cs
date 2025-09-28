using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Soft1_To_Atum.Data.Services;

public class DatabaseService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IServiceProvider serviceProvider, ILogger<DatabaseService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring database is created...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        try
        {
            // Ensure database exists first
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            // Check if AppSettings table exists and create it if missing
            try
            {
                // Test if AppSettings table exists by trying to query it
                var testQuery = await dbContext.AppSettings.AnyAsync(cancellationToken);
                _logger.LogDebug("AppSettings table exists and is accessible");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such table: AppSettings"))
            {
                _logger.LogInformation("AppSettings table missing, creating it...");

                // Create AppSettings table with proper migration
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE ""AppSettings"" (
                        ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_AppSettings"" PRIMARY KEY,
                        ""StoreName"" TEXT NOT NULL,
                        ""StoreEnabled"" INTEGER NOT NULL,
                        ""SoftOneGoBaseUrl"" TEXT NOT NULL,
                        ""SoftOneGoAppId"" TEXT NOT NULL,
                        ""SoftOneGoToken"" TEXT NOT NULL,
                        ""SoftOneGoS1Code"" TEXT NOT NULL,
                        ""SoftOneGoFilters"" TEXT NOT NULL,
                        ""WooCommerceUrl"" TEXT NOT NULL,
                        ""WooCommerceConsumerKey"" TEXT NOT NULL,
                        ""WooCommerceConsumerSecret"" TEXT NOT NULL,
                        ""WooCommerceVersion"" TEXT NOT NULL,
                        ""AtumLocationId"" INTEGER NOT NULL,
                        ""AtumLocationName"" TEXT NOT NULL,
                        ""EmailSmtpHost"" TEXT NOT NULL,
                        ""EmailSmtpPort"" INTEGER NOT NULL,
                        ""EmailUsername"" TEXT NOT NULL,
                        ""EmailPassword"" TEXT NOT NULL,
                        ""EmailFromEmail"" TEXT NOT NULL,
                        ""EmailToEmail"" TEXT NOT NULL,
                        ""SyncIntervalMinutes"" INTEGER NOT NULL,
                        ""SyncAutoSync"" INTEGER NOT NULL,
                        ""SyncEmailNotifications"" INTEGER NOT NULL,
                        ""MatchingPrimaryField"" TEXT NOT NULL,
                        ""MatchingSecondaryField"" TEXT NOT NULL,
                        ""MatchingCreateMissingProducts"" INTEGER NOT NULL,
                        ""MatchingUpdateExistingProducts"" INTEGER NOT NULL,
                        ""FieldMappingSku"" TEXT NOT NULL,
                        ""FieldMappingName"" TEXT NOT NULL,
                        ""FieldMappingPrice"" TEXT NOT NULL,
                        ""FieldMappingStockQuantity"" TEXT NOT NULL,
                        ""FieldMappingCategory"" TEXT NOT NULL,
                        ""FieldMappingUnit"" TEXT NOT NULL,
                        ""FieldMappingVat"" TEXT NOT NULL,
                        ""CreatedAt"" TEXT NOT NULL,
                        ""UpdatedAt"" TEXT NOT NULL
                    );", cancellationToken);

                // Insert default settings
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO ""AppSettings"" (
                        ""Id"", ""StoreName"", ""StoreEnabled"",
                        ""SoftOneGoBaseUrl"", ""SoftOneGoAppId"", ""SoftOneGoToken"", ""SoftOneGoS1Code"", ""SoftOneGoFilters"",
                        ""WooCommerceUrl"", ""WooCommerceConsumerKey"", ""WooCommerceConsumerSecret"", ""WooCommerceVersion"",
                        ""AtumLocationId"", ""AtumLocationName"",
                        ""EmailSmtpHost"", ""EmailSmtpPort"", ""EmailUsername"", ""EmailPassword"", ""EmailFromEmail"", ""EmailToEmail"",
                        ""SyncIntervalMinutes"", ""SyncAutoSync"", ""SyncEmailNotifications"",
                        ""MatchingPrimaryField"", ""MatchingSecondaryField"", ""MatchingCreateMissingProducts"", ""MatchingUpdateExistingProducts"",
                        ""FieldMappingSku"", ""FieldMappingName"", ""FieldMappingPrice"", ""FieldMappingStockQuantity"", ""FieldMappingCategory"", ""FieldMappingUnit"", ""FieldMappingVat"",
                        ""CreatedAt"", ""UpdatedAt""
                    ) VALUES (
                        1, 'Κατάστημα Κέντρο', 1,
                        'https://go.s1cloud.net/s1services', '703', '', '', 'ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999',
                        '', '', '', 'wc/v3',
                        870, 'store1_location',
                        '', 587, '', '', '', '',
                        15, 1, 1,
                        'sku', 'barcode', 1, 1,
                        'ITEM.CODE1', 'ITEM.NAME', 'ITEM.PRICER', 'ITEM.MTRL_ITEMTRDATA_QTY1', 'ITEM.MTRCATEGORY', 'ITEM.MTRUNIT1', 'ITEM.VAT',
                        datetime('now'), datetime('now')
                    );", cancellationToken);

                _logger.LogInformation("AppSettings table created successfully with default data");
            }

            _logger.LogInformation("Database created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}