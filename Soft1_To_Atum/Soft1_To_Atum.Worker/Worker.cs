using Microsoft.EntityFrameworkCore;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;

namespace Soft1_To_Atum.Worker;

public class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SyncWorker(ILogger<SyncWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan syncInterval = TimeSpan.FromMinutes(15); // Default fallback

        // Get sync interval from database
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var settings = await settingsService.GetAppSettingsAsync();

            if (settings.SyncAutoSync)
            {
                syncInterval = TimeSpan.FromMinutes(settings.SyncIntervalMinutes);
                _logger.LogInformation("SyncWorker started. Auto-sync enabled. Sync interval: {interval} minutes", syncInterval.TotalMinutes);
            }
            else
            {
                _logger.LogInformation("SyncWorker started. Auto-sync is DISABLED. Worker will check settings every 5 minutes.");
                syncInterval = TimeSpan.FromMinutes(5); // Check settings every 5 minutes when auto-sync is disabled
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading settings from database, using default interval of 15 minutes");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Re-read settings to check if they changed
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
                var currentSettings = await settingsService.GetAppSettingsAsync();

                if (currentSettings.SyncAutoSync)
                {
                    // Update sync interval if it changed
                    var newInterval = TimeSpan.FromMinutes(currentSettings.SyncIntervalMinutes);
                    if (newInterval != syncInterval)
                    {
                        syncInterval = newInterval;
                        _logger.LogInformation("Sync interval updated to {interval} minutes", syncInterval.TotalMinutes);
                    }

                    // Perform sync
                    await PerformSyncAsync(stoppingToken, currentSettings);
                }
                else
                {
                    _logger.LogDebug("Auto-sync is disabled, skipping sync operation");
                    syncInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes when disabled
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync operation");
            }

            await Task.Delay(syncInterval, stoppingToken);
        }
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken, AppSettings settings)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var syncLog = new SyncLog
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            TotalProducts = 0,
            CreatedProducts = 0,
            UpdatedProducts = 0,
            SkippedProducts = 0,
            ErrorCount = 0
        };

        dbContext.SyncLogs.Add(syncLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Starting sync operation with store: {StoreName}. SyncLog ID: {syncLogId}",
            settings.StoreName, syncLog.Id);

        try
        {
            // Validate required settings
            if (string.IsNullOrEmpty(settings.SoftOneGoBaseUrl) || string.IsNullOrEmpty(settings.SoftOneGoToken))
            {
                throw new InvalidOperationException("SoftOne Go settings are not configured properly");
            }

            if (string.IsNullOrEmpty(settings.WooCommerceUrl) || string.IsNullOrEmpty(settings.WooCommerceConsumerKey))
            {
                throw new InvalidOperationException("WooCommerce settings are not configured properly");
            }

            _logger.LogInformation("Using SoftOne Go API: {BaseUrl}", settings.SoftOneGoBaseUrl);
            _logger.LogInformation("Using WooCommerce API: {Url}", settings.WooCommerceUrl);
            _logger.LogInformation("Using ATUM Location: {LocationId} ({LocationName})",
                settings.AtumLocationId, settings.AtumLocationName);

            // 1. Fetch products from SoftOne Go API
            var softOneService = scope.ServiceProvider.GetRequiredService<SoftOneApiService>();
            var softOneProducts = await softOneService.GetProductsAsync(
                settings.SoftOneGoBaseUrl,
                settings.SoftOneGoAppId,
                settings.SoftOneGoToken,
                settings.SoftOneGoS1Code,
                settings.SoftOneGoFilters,
                cancellationToken);

            _logger.LogInformation("Fetched {ProductCount} products from SoftOne Go API", softOneProducts.Count);
            syncLog.TotalProducts = softOneProducts.Count;

            if (softOneProducts.Count == 0)
            {
                _logger.LogWarning("No products found in SoftOne Go API with filters: {Filters}", settings.SoftOneGoFilters);
                syncLog.Status = "Completed";
                syncLog.CompletedAt = DateTime.UtcNow;
                return;
            }

            // 2. Get WooCommerce client
            var wooCommerceClient = scope.ServiceProvider.GetRequiredService<IWooCommerceAtumClient>();

            // Find the store to sync with (assuming we're using the first active store)
            var store = await dbContext.Stores.FirstOrDefaultAsync(s => s.IsActive, cancellationToken);
            if (store == null)
            {
                throw new InvalidOperationException("No active WooCommerce store found. Please configure a store first.");
            }

            _logger.LogInformation("Syncing to WooCommerce store: {StoreName} ({StoreUrl})", store.Name, store.WooCommerceUrl);

            // 3. Process each SoftOne product
            foreach (var softOneProduct in softOneProducts)
            {
                try
                {
                    await ProcessProductAsync(softOneProduct, store.Id, wooCommerceClient, dbContext, settings, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing SoftOne product {ProductCode}: {Error}", softOneProduct.Code, ex.Message);
                    syncLog.ErrorCount++;
                }
            }

            // Calculate final statistics
            var totalProcessed = syncLog.CreatedProducts + syncLog.UpdatedProducts + syncLog.SkippedProducts;
            _logger.LogInformation("Processed {Processed}/{Total} products", totalProcessed, syncLog.TotalProducts);
            syncLog.Status = "Completed";
            syncLog.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Sync completed for store: {StoreName}. Duration: {duration}, Products: {total} ({created} created, {updated} updated, {skipped} skipped, {errors} errors)",
                settings.StoreName, syncLog.Duration?.ToString(@"hh\:mm\:ss"),
                syncLog.TotalProducts, syncLog.CreatedProducts, syncLog.UpdatedProducts, syncLog.SkippedProducts, syncLog.ErrorCount);

            // 4. Send email notification if enabled
            if (settings.SyncEmailNotifications && !string.IsNullOrEmpty(settings.EmailToEmail))
            {
                try
                {
                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                    await emailService.SendSyncNotificationAsync(syncLog, settings, cancellationToken);
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Failed to send email notification: {Error}", emailEx.Message);
                }
            }
        }
        catch (Exception ex)
        {
            syncLog.Status = "Failed";
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.ErrorDetails = ex.Message;
            syncLog.ErrorCount++;

            _logger.LogError(ex, "Sync operation failed");
            throw;
        }
        finally
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessProductAsync(
        SoftOneProduct softOneProduct,
        int storeId,
        IWooCommerceAtumClient wooCommerceClient,
        SyncDbContext dbContext,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        // Determine the SKU to use for matching
        var sku = GetProductSku(softOneProduct, settings);
        if (string.IsNullOrEmpty(sku))
        {
            _logger.LogWarning("No valid SKU found for SoftOne product {ProductCode}", softOneProduct.Code);
            await IncrementSkippedCount(dbContext);
            return;
        }

        _logger.LogDebug("Processing SoftOne product {ProductCode} with SKU {Sku}", softOneProduct.Code, sku);

        // Check if product exists in WooCommerce
        var existingProduct = await wooCommerceClient.GetProductBySkuAsync(storeId, sku, cancellationToken);

        if (existingProduct != null)
        {
            // Product exists - update if enabled
            if (settings.MatchingUpdateExistingProducts)
            {
                await UpdateExistingProduct(existingProduct, softOneProduct, storeId, wooCommerceClient, dbContext, settings, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Product {Sku} exists but updating is disabled. Skipping.", sku);
                await IncrementSkippedCount(dbContext);
            }
        }
        else
        {
            // Product doesn't exist - create if enabled
            if (settings.MatchingCreateMissingProducts)
            {
                await CreateNewProduct(softOneProduct, storeId, wooCommerceClient, dbContext, settings, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Product {Sku} doesn't exist but creation is disabled. Skipping.", sku);
                await IncrementSkippedCount(dbContext);
            }
        }
    }

    private string GetProductSku(SoftOneProduct softOneProduct, AppSettings settings)
    {
        // Use primary field first, then fallback to secondary
        var primaryValue = GetFieldValue(softOneProduct, settings.MatchingPrimaryField);
        if (!string.IsNullOrEmpty(primaryValue))
            return primaryValue;

        var secondaryValue = GetFieldValue(softOneProduct, settings.MatchingSecondaryField);
        return secondaryValue ?? string.Empty;
    }

    private string? GetFieldValue(SoftOneProduct product, string fieldName)
    {
        return fieldName.ToLower() switch
        {
            "sku" or "code" => product.Code,
            "barcode" => product.Barcode,
            "name" => product.Name,
            _ => null
        };
    }

    private async Task CreateNewProduct(
        SoftOneProduct softOneProduct,
        int storeId,
        IWooCommerceAtumClient wooCommerceClient,
        SyncDbContext dbContext,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var sku = GetProductSku(softOneProduct, settings);

        var createRequest = new CreateWooCommerceProductRequest
        {
            Name = softOneProduct.Name,
            Sku = sku,
            RegularPrice = softOneProduct.RetailPrice ?? 0,
            Description = $"Imported from SoftOne Go - Code: {softOneProduct.Code}",
            StockQuantity = (int)(softOneProduct.StockQuantity ?? 0),
            ManageStock = true
        };

        var createdProduct = await wooCommerceClient.CreateProductAsync(storeId, createRequest, cancellationToken);

        // Update ATUM inventory if location is configured
        if (settings.AtumLocationId > 0)
        {
            var inventoryRequest = new UpdateAtumInventoryRequest
            {
                Quantity = (int)(softOneProduct.StockQuantity ?? 0),
                Location = settings.AtumLocationName
            };
            await wooCommerceClient.UpdateAtumInventoryAsync(storeId, createdProduct.Id, inventoryRequest, cancellationToken);
        }

        // Save to local database
        await SaveProductToDatabase(softOneProduct, createdProduct, dbContext, cancellationToken);

        await IncrementCreatedCount(dbContext);
        _logger.LogInformation("Created new product {Sku} (WooCommerce ID: {ProductId})", sku, createdProduct.Id);
    }

    private async Task UpdateExistingProduct(
        WooCommerceProduct existingProduct,
        SoftOneProduct softOneProduct,
        int storeId,
        IWooCommerceAtumClient wooCommerceClient,
        SyncDbContext dbContext,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var updateRequest = new UpdateWooCommerceProductRequest
        {
            Name = softOneProduct.Name,
            RegularPrice = softOneProduct.RetailPrice ?? existingProduct.RegularPrice,
            StockQuantity = (int)(softOneProduct.StockQuantity ?? existingProduct.StockQuantity)
        };

        var updatedProduct = await wooCommerceClient.UpdateProductAsync(storeId, existingProduct.Id, updateRequest, cancellationToken);

        // Update ATUM inventory if location is configured
        if (settings.AtumLocationId > 0)
        {
            var inventoryRequest = new UpdateAtumInventoryRequest
            {
                Quantity = (int)(softOneProduct.StockQuantity ?? 0),
                Location = settings.AtumLocationName
            };
            await wooCommerceClient.UpdateAtumInventoryAsync(storeId, updatedProduct.Id, inventoryRequest, cancellationToken);
        }

        // Update local database
        await UpdateProductInDatabase(softOneProduct, updatedProduct, dbContext, cancellationToken);

        await IncrementUpdatedCount(dbContext);
        _logger.LogInformation("Updated existing product {Sku} (WooCommerce ID: {ProductId})", existingProduct.Sku, existingProduct.Id);
    }

    private async Task SaveProductToDatabase(SoftOneProduct softOneProduct, WooCommerceProduct wooProduct, SyncDbContext dbContext, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            SoftOneId = softOneProduct.Code,
            WooCommerceId = wooProduct.Id.ToString(),
            AtumId = wooProduct.Id.ToString(),
            Name = wooProduct.Name,
            Sku = wooProduct.Sku,
            Price = wooProduct.RegularPrice,
            Quantity = wooProduct.StockQuantity,
            LastSyncedAt = DateTime.UtcNow,
            LastSyncStatus = "Created"
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateProductInDatabase(SoftOneProduct softOneProduct, WooCommerceProduct wooProduct, SyncDbContext dbContext, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.WooCommerceId == wooProduct.Id.ToString(), cancellationToken);

        if (product != null)
        {
            product.Name = wooProduct.Name;
            product.Price = wooProduct.RegularPrice;
            product.Quantity = wooProduct.StockQuantity;
            product.LastSyncedAt = DateTime.UtcNow;
            product.LastSyncStatus = "Updated";
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task IncrementCreatedCount(SyncDbContext dbContext)
    {
        var syncLog = await dbContext.SyncLogs
            .OrderByDescending(s => s.StartedAt)
            .FirstAsync();
        syncLog.CreatedProducts++;
        await dbContext.SaveChangesAsync();
    }

    private async Task IncrementUpdatedCount(SyncDbContext dbContext)
    {
        var syncLog = await dbContext.SyncLogs
            .OrderByDescending(s => s.StartedAt)
            .FirstAsync();
        syncLog.UpdatedProducts++;
        await dbContext.SaveChangesAsync();
    }

    private async Task IncrementSkippedCount(SyncDbContext dbContext)
    {
        var syncLog = await dbContext.SyncLogs
            .OrderByDescending(s => s.StartedAt)
            .FirstAsync();
        syncLog.SkippedProducts++;
        await dbContext.SaveChangesAsync();
    }
}
