using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;

namespace Soft1_To_Atum.WindowsService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SyncServiceConfiguration _config;
    private Timer? _syncTimer;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IOptions<SyncServiceConfiguration> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Soft1ToAtumSyncService started at: {Time}", DateTimeOffset.Now);
        _logger.LogInformation("Sync interval: {Interval} minutes", _config.SyncSettings.IntervalMinutes);
        _logger.LogInformation("Auto-sync enabled: {Enabled}", _config.SyncSettings.EnableAutoSync);

        if (!_config.SyncSettings.EnableAutoSync)
        {
            _logger.LogInformation("Auto-sync is disabled. Service will not perform automatic synchronizations.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        // Initial sync on startup (wait 10 seconds first)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await PerformSync();

        // Setup periodic sync
        var interval = TimeSpan.FromMinutes(_config.SyncSettings.IntervalMinutes);
        _syncTimer = new Timer(
            async _ => await PerformSync(),
            null,
            interval,
            interval);

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task PerformSync()
    {
        using var scope = _serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Worker>>();

        try
        {
            logger.LogInformation("=== Starting Sync Cycle at {Time} ===", DateTimeOffset.Now);

            var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
            var softOneService = scope.ServiceProvider.GetRequiredService<SoftOneApiService>();
            var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceApiService>();
            var atumService = scope.ServiceProvider.GetRequiredService<AtumApiService>();

            // Step 1: Read from SoftOne
            logger.LogInformation("Step 1: Reading products from SoftOne...");
            var softOneProducts = await softOneService.GetProductsAsync(
                _config.SoftOne.BaseUrl,
                _config.SoftOne.AppId,
                _config.SoftOne.Token,
                _config.SoftOne.S1Code,
                "", // filters - empty for now
                default);

            logger.LogInformation("Retrieved {Count} products from SoftOne", softOneProducts.Count);

            // Step 2: Save to database
            logger.LogInformation("Step 2: Saving to database...");
            int savedCount = 0;
            foreach (var sp in softOneProducts)
            {
                var existing = await db.Products.FirstOrDefaultAsync(p => p.Sku == sp.Code);
                if (existing != null)
                {
                    existing.Quantity = sp.StockQuantity ?? 0;
                    existing.Price = sp.RetailPrice ?? 0;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.Products.Add(new Product
                    {
                        SoftOneId = sp.Code,
                        InternalId = sp.InternalId,
                        Sku = sp.Code,
                        Name = sp.Name,
                        Barcode = sp.Barcode,
                        Quantity = sp.StockQuantity ?? 0,
                        Price = sp.RetailPrice ?? 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                savedCount++;
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Saved {Count} products to database", savedCount);

            // Step 3: WooCommerce matching/creation (parallel processing)
            logger.LogInformation("Step 3: WooCommerce matching and creation...");
            var allProducts = await db.Products.ToListAsync();
            var productsWithoutWooId = allProducts.Where(p => string.IsNullOrEmpty(p.WooCommerceId)).ToList();
            logger.LogInformation("Found {Count} products without WooCommerce ID", productsWithoutWooId.Count);

            int matchedCount = 0;
            int createdCount = 0;
            int errorCount = 0;
            var semaphore = new SemaphoreSlim(10, 10);
            var tasks = new List<Task>();

            foreach (var product in productsWithoutWooId)
            {
                if (string.IsNullOrEmpty(product.Sku))
                {
                    logger.LogWarning("Product {Name} has no SKU, skipping", product.Name);
                    errorCount++;
                    continue;
                }

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var wooProduct = await wooCommerceService.GetProductBySkuAsync(
                            _config.WooCommerce.ConsumerKey,
                            _config.WooCommerce.ConsumerSecret,
                            product.Sku,
                            default);

                        if (wooProduct != null && wooProduct.Id > 0)
                        {
                            product.WooCommerceId = wooProduct.Id.ToString();
                            product.UpdatedAt = DateTime.UtcNow;
                            product.LastSyncStatus = "Matched in WooCommerce";
                            Interlocked.Increment(ref matchedCount);
                            logger.LogInformation("Matched product {Name} (SKU: {Sku}) -> WooCommerce ID: {WooId}",
                                product.Name, product.Sku, wooProduct.Id);
                        }
                        else
                        {
                            logger.LogInformation("Creating draft product in WooCommerce: {Name} (SKU: {Sku})",
                                product.Name, product.Sku);

                            var newProduct = await wooCommerceService.CreateProductAsync(
                                _config.WooCommerce.ConsumerKey,
                                _config.WooCommerce.ConsumerSecret,
                                product.Name,
                                product.Sku,
                                product.Price,
                                default);

                            if (newProduct != null && newProduct.Id > 0)
                            {
                                product.WooCommerceId = newProduct.Id.ToString();
                                product.UpdatedAt = DateTime.UtcNow;
                                product.LastSyncStatus = "Created as draft in WooCommerce";
                                Interlocked.Increment(ref createdCount);
                                logger.LogInformation("Created draft product {Name} -> WooCommerce ID: {WooId}",
                                    product.Name, newProduct.Id);
                            }
                            else
                            {
                                product.LastSyncStatus = "Error - Failed to create in WooCommerce";
                                Interlocked.Increment(ref errorCount);
                                logger.LogWarning("Failed to create product {Name} in WooCommerce", product.Name);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing product {Name} (SKU: {Sku})", product.Name, product.Sku);
                        product.LastSyncStatus = $"Error - {ex.Message}";
                        Interlocked.Increment(ref errorCount);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await db.SaveChangesAsync();
            logger.LogInformation("WooCommerce matching complete: {Matched} matched, {Created} created, {Errors} errors",
                matchedCount, createdCount, errorCount);

            // Step 4: ATUM inventory creation (batch processing)
            logger.LogInformation("Step 4: ATUM inventory creation...");
            var productsForAtum = allProducts
                .Where(p => !string.IsNullOrEmpty(p.WooCommerceId) && string.IsNullOrEmpty(p.AtumId))
                .ToList();

            logger.LogInformation("Found {Count} products to create in ATUM", productsForAtum.Count);

            int createdInAtum = 0;
            int atumErrors = 0;

            if (productsForAtum.Any())
            {
                const int batchSize = 50;
                int totalBatches = (int)Math.Ceiling((double)productsForAtum.Count / batchSize);

                for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                {
                    var currentBatchProducts = productsForAtum
                        .Skip(batchIndex * batchSize)
                        .Take(batchSize)
                        .ToList();

                    logger.LogInformation("Processing ATUM creation batch {Current}/{Total} ({Count} products)",
                        batchIndex + 1, totalBatches, currentBatchProducts.Count);

                    var batchRequest = new AtumBatchRequest();

                    foreach (var product in currentBatchProducts)
                    {
                        if (!int.TryParse(product.WooCommerceId, out int wooCommerceProductId))
                        {
                            logger.LogWarning("Invalid WooCommerce ID for product {Name}: {WooId}", product.Name, product.WooCommerceId);
                            atumErrors++;
                            continue;
                        }

                        var createItem = new AtumBatchCreateItem
                        {
                            ProductId = wooCommerceProductId,
                            Name = _config.ATUM.LocationName ?? "store1_location",
                            IsMain = false,
                            Location = new List<int> { _config.ATUM.LocationId },
                            MetaData = new AtumBatchMetaData
                            {
                                Sku = product.Sku ?? "",
                                ManageStock = true,
                                StockQuantity = product.Quantity,
                                Backorders = false,
                                StockStatus = product.Quantity > 0 ? "instock" : "outofstock",
                                Barcode = product.Barcode ?? ""
                            }
                        };

                        batchRequest.Create.Add(createItem);
                    }

                    if (batchRequest.Create.Any())
                    {
                        try
                        {
                            var batchResponse = await atumService.BatchUpdateInventoryAsync(
                                _config.WooCommerce.ConsumerKey,
                                _config.WooCommerce.ConsumerSecret,
                                batchRequest,
                                default);

                            if (batchResponse.Create != null && batchResponse.Create.Any())
                            {
                                foreach (var createdItem in batchResponse.Create)
                                {
                                    if (createdItem.Error != null)
                                    {
                                        logger.LogError("ATUM creation error for product {ProductId}: {ErrorCode} - {ErrorMessage}",
                                            createdItem.ProductId, createdItem.Error.Code, createdItem.Error.Message);
                                        atumErrors++;
                                        continue;
                                    }

                                    var product = allProducts.FirstOrDefault(p => p.WooCommerceId == createdItem.ProductId.ToString());
                                    if (product != null)
                                    {
                                        product.AtumId = createdItem.Id.ToString();
                                        product.AtumQuantity = product.Quantity;
                                        product.UpdatedAt = DateTime.UtcNow;
                                        product.LastSyncedAt = DateTime.UtcNow;
                                        product.LastSyncStatus = "Created in ATUM";
                                        createdInAtum++;
                                    }
                                }

                                await db.SaveChangesAsync();
                                logger.LogInformation("Batch {Current} completed: {Created} created",
                                    batchIndex + 1, batchResponse.Create.Count);
                            }
                        }
                        catch (Exception batchEx)
                        {
                            logger.LogError(batchEx, "Error processing ATUM batch {BatchNum}: {Message}",
                                batchIndex + 1, batchEx.Message);
                            atumErrors += currentBatchProducts.Count;
                        }

                        if (batchIndex < totalBatches - 1)
                        {
                            await Task.Delay(500);
                        }
                    }
                }

                logger.LogInformation("ATUM creation complete: {Created} created, {Errors} errors",
                    createdInAtum, atumErrors);
            }

            // Step 5: ATUM quantity updates (batch processing, skip if quantity unchanged)
            logger.LogInformation("Step 5: ATUM quantity updates...");
            var productsForAtumUpdate = allProducts
                .Where(p => (!string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId)) &&
                           !string.IsNullOrEmpty(p.WooCommerceId) &&
                           !string.IsNullOrEmpty(p.AtumId) &&
                           p.Quantity != p.AtumQuantity) // Skip if quantity unchanged
                .ToList();

            logger.LogInformation("Found {Count} products with quantity changes to update in ATUM", productsForAtumUpdate.Count);

            int updatedInAtum = 0;
            int atumUpdateErrors = 0;
            int skippedCount = allProducts.Count(p => !string.IsNullOrEmpty(p.AtumId) && p.Quantity == p.AtumQuantity);

            logger.LogInformation("Skipping {Count} products with unchanged quantities", skippedCount);

            if (productsForAtumUpdate.Any())
            {
                const int batchSize = 50;
                int totalBatches = (int)Math.Ceiling((double)productsForAtumUpdate.Count / batchSize);

                for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                {
                    var currentBatchProducts = productsForAtumUpdate
                        .Skip(batchIndex * batchSize)
                        .Take(batchSize)
                        .ToList();

                    logger.LogInformation("Processing ATUM update batch {Current}/{Total} ({Count} products)",
                        batchIndex + 1, totalBatches, currentBatchProducts.Count);

                    var batchRequest = new AtumBatchRequest();

                    foreach (var product in currentBatchProducts)
                    {
                        if (!int.TryParse(product.AtumId, out int atumInventoryId))
                        {
                            logger.LogWarning("Invalid ATUM ID for product {Name}: {AtumId}", product.Name, product.AtumId);
                            atumUpdateErrors++;
                            continue;
                        }

                        var updateItem = new AtumBatchUpdateItem
                        {
                            Id = atumInventoryId,
                            MetaData = new AtumBatchUpdateMetaData
                            {
                                StockQuantity = product.Quantity
                            }
                        };

                        batchRequest.Update.Add(updateItem);
                    }

                    if (batchRequest.Update.Any())
                    {
                        try
                        {
                            var batchResponse = await atumService.BatchUpdateInventoryAsync(
                                _config.WooCommerce.ConsumerKey,
                                _config.WooCommerce.ConsumerSecret,
                                batchRequest,
                                default);

                            if (batchResponse.Update != null && batchResponse.Update.Any())
                            {
                                foreach (var updatedItem in batchResponse.Update)
                                {
                                    if (updatedItem.Error != null)
                                    {
                                        logger.LogError("ATUM update error for inventory {InventoryId}: {ErrorCode} - {ErrorMessage}",
                                            updatedItem.Id, updatedItem.Error.Code, updatedItem.Error.Message);
                                        atumUpdateErrors++;
                                        continue;
                                    }

                                    var product = allProducts.FirstOrDefault(p => p.AtumId == updatedItem.Id.ToString());
                                    if (product != null)
                                    {
                                        product.AtumQuantity = product.Quantity;
                                        product.UpdatedAt = DateTime.UtcNow;
                                        product.LastSyncedAt = DateTime.UtcNow;
                                        product.LastSyncStatus = "ATUM: Quantity updated";
                                        updatedInAtum++;
                                    }
                                }

                                await db.SaveChangesAsync();
                                logger.LogInformation("Batch {Current} completed: {Updated} updated",
                                    batchIndex + 1, batchResponse.Update.Count);
                            }
                        }
                        catch (Exception batchEx)
                        {
                            logger.LogError(batchEx, "Error processing ATUM update batch {BatchNum}: {Message}",
                                batchIndex + 1, batchEx.Message);
                            atumUpdateErrors += currentBatchProducts.Count;
                        }

                        if (batchIndex < totalBatches - 1)
                        {
                            await Task.Delay(500);
                        }
                    }
                }

                logger.LogInformation("ATUM update complete: {Updated} updated, {Skipped} skipped (unchanged), {Errors} errors",
                    updatedInAtum, skippedCount, atumUpdateErrors);
            }

            logger.LogInformation("=== Sync Cycle Completed Successfully at {Time} ===", DateTimeOffset.Now);
            logger.LogInformation("Summary: SoftOne={SoftOne}, WooCommerce Matched={Matched}, WooCommerce Created={Created}, ATUM Created={AtumCreated}, ATUM Updated={AtumUpdated}",
                softOneProducts.Count, matchedCount, createdCount, createdInAtum, updatedInAtum);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during sync cycle: {Message}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Soft1ToAtumSyncService is stopping at: {Time}", DateTimeOffset.Now);
        _syncTimer?.Dispose();
        await base.StopAsync(stoppingToken);
    }
}
