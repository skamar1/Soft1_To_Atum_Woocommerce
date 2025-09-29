using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data;
using Microsoft.EntityFrameworkCore;

namespace Soft1_To_Atum.ApiService.Services;

public class AtumBatchService
{
    private readonly SyncDbContext _context;
    private readonly ILogger<AtumBatchService> _logger;

    public AtumBatchService(SyncDbContext context, ILogger<AtumBatchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AtumBatchRequest> PrepareAtumBatchRequestAsync(int maxBatchSize = 50)
    {
        _logger.LogInformation("=== PREPARING ATUM BATCH REQUEST ===");

        var allProducts = await _context.Products.ToListAsync();
        _logger.LogInformation("Loaded {TotalProducts} products from database", allProducts.Count);

        // Log products with ATUM IDs for debugging
        var productsWithAtum = allProducts.Where(p => !string.IsNullOrEmpty(p.AtumId)).ToList();
        _logger.LogInformation("Found {AtumProductCount} products with ATUM IDs", productsWithAtum.Count);

        // Log a few examples
        foreach (var product in productsWithAtum.Take(5))
        {
            _logger.LogDebug("Sample product: {Name} - SoftOne: {SoftOne}, ATUM: {Atum}, AtumId: {AtumId}",
                product.Name, product.Quantity, product.AtumQuantity, product.AtumId);
        }

        var createItems = new List<AtumBatchCreateItem>();
        var updateItems = new List<AtumBatchUpdateItem>();
        var deleteItems = new List<int>();

        // Products that exist in SoftOne (have quantity > 0) but not in ATUM - CREATE
        var productsToCreate = allProducts.Where(p =>
            (!string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId)) &&
            p.Quantity > 0 &&
            string.IsNullOrEmpty(p.AtumId)).ToList();

        foreach (var product in productsToCreate)
        {
            // Skip products that don't have valid data
            if (string.IsNullOrEmpty(product.Sku))
            {
                _logger.LogWarning("Product {Name} has no SKU. Marking as error.", product.Name);
                product.LastSyncStatus = "Error - No SKU";
                product.LastSyncedAt = DateTime.UtcNow;
                continue;
            }

            var createItem = new AtumBatchCreateItem
            {
                Name = !string.IsNullOrEmpty(product.Name) ? product.Name : $"Product {product.Sku}",
                MetaData = new AtumBatchMetaData
                {
                    Sku = product.Sku,
                    StockQuantity = Math.Max(0, (int)product.Quantity),
                    ManageStock = true
                }
            };

            createItems.Add(createItem);
        }

        // Products that exist in ATUM - UPDATE
        var productsToUpdate = allProducts.Where(p => !string.IsNullOrEmpty(p.AtumId)).ToList();

        foreach (var product in productsToUpdate)
        {
            // Check if AtumId is valid (not 0)
            if (!int.TryParse(product.AtumId, out int atumId) || atumId <= 0)
            {
                _logger.LogWarning("Product {Name} (SKU: {Sku}) has invalid ATUM ID: {AtumId}. Marking as error.",
                    product.Name, product.Sku, product.AtumId);

                // Mark product as having issues
                product.LastSyncStatus = "Error - Invalid ATUM ID";
                product.LastSyncedAt = DateTime.UtcNow;
                continue;
            }

            // Determine the target quantity
            int targetQuantity;
            if (!string.IsNullOrEmpty(product.SoftOneId) || !string.IsNullOrEmpty(product.InternalId))
            {
                // Product exists in SoftOne - use SoftOne quantity (minimum 0)
                targetQuantity = Math.Max(0, (int)product.Quantity);
            }
            else
            {
                // Product exists in ATUM but not in SoftOne - set to 0
                targetQuantity = 0;
            }

            // Always update if there's any difference in quantities or if we need to sync all ATUM products
            var currentAtumQuantity = (int)product.AtumQuantity;

            _logger.LogDebug("Product {Name} (SKU: {Sku}): ATUM={CurrentAtum}, Target={Target}, SoftOne={SoftOne}",
                product.Name, product.Sku, currentAtumQuantity, targetQuantity, (int)product.Quantity);

            if (currentAtumQuantity != targetQuantity)
            {
                var updateItem = new AtumBatchUpdateItem
                {
                    Id = atumId,
                    StockQuantity = targetQuantity
                };

                updateItems.Add(updateItem);

                _logger.LogInformation("Queued product {Name} for update: {CurrentAtum} → {Target}",
                    product.Name, currentAtumQuantity, targetQuantity);
            }
            else
            {
                _logger.LogDebug("Product {Name} already in sync: {Quantity}", product.Name, currentAtumQuantity);
            }
        }

        // Limit batch size to prevent "Request Entity Too Large" errors
        var totalItems = createItems.Count + updateItems.Count;
        if (totalItems > maxBatchSize)
        {
            _logger.LogWarning("Batch request has {TotalItems} items, which exceeds max batch size of {MaxBatchSize}. Limiting to first {MaxBatchSize} items.",
                totalItems, maxBatchSize, maxBatchSize);

            // Prioritize updates over creates, then limit by batch size
            var finalCreateItems = new List<AtumBatchCreateItem>();
            var finalUpdateItems = new List<AtumBatchUpdateItem>();

            // Take updates first (they're more important)
            var updatesTaken = Math.Min(updateItems.Count, maxBatchSize);
            finalUpdateItems.AddRange(updateItems.Take(updatesTaken));

            // Take creates with remaining capacity
            var remainingCapacity = maxBatchSize - updatesTaken;
            if (remainingCapacity > 0)
            {
                finalCreateItems.AddRange(createItems.Take(remainingCapacity));
            }

            createItems = finalCreateItems;
            updateItems = finalUpdateItems;
        }

        _logger.LogInformation("Prepared batch request: {CreateCount} creates, {UpdateCount} updates (max batch size: {MaxBatchSize})",
            createItems.Count, updateItems.Count, maxBatchSize);

        // Save any changes made to product statuses during validation
        await _context.SaveChangesAsync();

        return new AtumBatchRequest
        {
            Create = createItems,
            Update = updateItems,
            Delete = deleteItems // Currently not used, but keeping for future use
        };
    }

    public async Task ProcessAtumBatchResponseAsync(AtumBatchResponse batchResponse)
    {
        _logger.LogInformation("=== PROCESSING ATUM BATCH RESPONSE ===");

        int createdCount = 0;
        int updatedCount = 0;
        int errorCount = 0;

        // Process created products
        if (batchResponse.Create?.Any() == true)
        {
            foreach (var createdProduct in batchResponse.Create)
            {
                if (createdProduct.Id > 0)
                {
                    // Find the corresponding product in our database by Name (since we don't have SKU in response)
                    var dbProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.Name == createdProduct.Name);

                    if (dbProduct != null)
                    {
                        dbProduct.AtumId = createdProduct.Id.ToString();
                        dbProduct.LastSyncedAt = DateTime.UtcNow;
                        dbProduct.LastSyncStatus = "Synced";

                        _logger.LogDebug("Updated product {Name} with ATUM ID {AtumId}",
                            dbProduct.Name, createdProduct.Id);

                        createdCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Could not find product with Name {Name} to update ATUM ID",
                            createdProduct.Name);
                        errorCount++;
                    }
                }
                else
                {
                    _logger.LogError("Created product response has invalid ID: {ProductData}",
                        System.Text.Json.JsonSerializer.Serialize(createdProduct));
                    errorCount++;
                }
            }
        }

        // Process updated products
        if (batchResponse.Update?.Any() == true)
        {
            foreach (var updatedProduct in batchResponse.Update)
            {
                if (updatedProduct.Id > 0)
                {
                    var dbProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.AtumId == updatedProduct.Id.ToString());

                    if (dbProduct != null)
                    {
                        dbProduct.LastSyncedAt = DateTime.UtcNow;
                        dbProduct.LastSyncStatus = "Synced";

                        _logger.LogDebug("Updated product {Name} ATUM inventory",
                            dbProduct.Name);

                        updatedCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Could not find product with ATUM ID {AtumId} to update",
                            updatedProduct.Id);
                        errorCount++;
                    }
                }
                else
                {
                    _logger.LogError("Updated product response has invalid ID: {ProductData}",
                        System.Text.Json.JsonSerializer.Serialize(updatedProduct));
                    errorCount++;
                }
            }
        }

        // Process errors
        if (batchResponse.Create?.Any(p => !string.IsNullOrEmpty(p.Error?.Message)) == true ||
            batchResponse.Update?.Any(p => !string.IsNullOrEmpty(p.Error?.Message)) == true)
        {
            foreach (var errorProduct in batchResponse.Create?.Where(p => !string.IsNullOrEmpty(p.Error?.Message)) ?? [])
            {
                _logger.LogError("ATUM batch create error for product {Name}: {Error}",
                    errorProduct.Name, errorProduct.Error?.Message);
                errorCount++;
            }

            foreach (var errorProduct in batchResponse.Update?.Where(p => !string.IsNullOrEmpty(p.Error?.Message)) ?? [])
            {
                _logger.LogError("ATUM batch update error for ID {Id}: {Error}",
                    errorProduct.Id, errorProduct.Error?.Message);
                errorCount++;
            }
        }

        // Save all changes to database
        var savedChanges = await _context.SaveChangesAsync();

        _logger.LogInformation("ATUM batch processing completed: {CreatedCount} created, {UpdatedCount} updated, {ErrorCount} errors, {SavedChanges} database changes",
            createdCount, updatedCount, errorCount, savedChanges);
    }

    public async Task<(int totalProducts, int needsSync, int differences)> GetAtumSyncStatisticsAsync()
    {
        var allProducts = await _context.Products.ToListAsync();
        var totalProducts = allProducts.Count;

        _logger.LogInformation("=== CALCULATING SYNC STATISTICS ===");
        _logger.LogInformation("Total products in database: {TotalProducts}", totalProducts);

        var productsWithAtum = allProducts.Where(p => !string.IsNullOrEmpty(p.AtumId)).ToList();
        _logger.LogInformation("Products with ATUM IDs: {AtumProductCount}", productsWithAtum.Count);

        // Products that need sync (different quantities between SoftOne and ATUM)
        var needsSync = 0;
        var differences = 0;

        foreach (var product in allProducts)
        {
            if (string.IsNullOrEmpty(product.AtumId))
            {
                // Product not in ATUM but exists in SoftOne with quantity > 0
                if ((!string.IsNullOrEmpty(product.SoftOneId) || !string.IsNullOrEmpty(product.InternalId)) && product.Quantity > 0)
                {
                    needsSync++;
                    _logger.LogDebug("Product {Name} needs creation in ATUM (SoftOne qty: {Qty})", product.Name, product.Quantity);
                }
            }
            else
            {
                // Product exists in ATUM
                var targetQuantity = (!string.IsNullOrEmpty(product.SoftOneId) || !string.IsNullOrEmpty(product.InternalId))
                    ? Math.Max(0, (int)product.Quantity)
                    : 0;

                var currentAtumQuantity = (int)product.AtumQuantity;

                if (currentAtumQuantity != targetQuantity)
                {
                    needsSync++;
                    differences++;
                    _logger.LogDebug("Product {Name} needs update: ATUM={Current} → Target={Target} (SoftOne={SoftOne})",
                        product.Name, currentAtumQuantity, targetQuantity, (int)product.Quantity);
                }
            }
        }

        _logger.LogInformation("Sync statistics: {NeedsSync} need sync, {Differences} quantity differences", needsSync, differences);
        return (totalProducts, needsSync, differences);
    }
}