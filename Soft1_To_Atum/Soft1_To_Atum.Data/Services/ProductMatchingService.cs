using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;
using System.Globalization;

namespace Soft1_To_Atum.Data.Services;

public class ProductMatchingService
{
    private readonly SyncDbContext _context;
    private readonly ILogger<ProductMatchingService> _logger;
    private readonly WooCommerceApiService _wooCommerceApiService;

    public ProductMatchingService(SyncDbContext context, ILogger<ProductMatchingService> logger, WooCommerceApiService wooCommerceApiService)
    {
        _context = context;
        _logger = logger;
        _wooCommerceApiService = wooCommerceApiService;
    }

    /// <summary>
    /// Processes a SoftOne product using the 3-level matching strategy:
    /// 1. Match by MTRL (InternalId) - all records
    /// 2. Match by CODE (Sku/Barcode) - only records without InternalId
    /// 3. Match by CODE1 (Sku/Barcode) - only records without InternalId
    /// 4. Create new if no match found
    /// </summary>
    public async Task<ProductMatchResult> ProcessSoftOneProductAsync(
        Dictionary<string, string?> softOneProduct,
        CancellationToken cancellationToken = default)
    {
        var mtrl = softOneProduct.GetValueOrDefault("ITEM.MTRL") ?? "";
        var code = softOneProduct.GetValueOrDefault("ITEM.CODE") ?? "";
        var code1 = softOneProduct.GetValueOrDefault("ITEM.CODE1") ?? "";

        _logger.LogDebug("Processing SoftOne product: MTRL={MTRL}, CODE={CODE}, CODE1={CODE1}",
            mtrl, code, code1);

        Product? existingProduct = null;
        string matchType = "";

        // Step 1: Try to match by MTRL (InternalId) - highest priority
        if (!string.IsNullOrEmpty(mtrl))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.InternalId == mtrl, cancellationToken);

            if (existingProduct != null)
            {
                matchType = "MTRL";
                _logger.LogDebug("Found existing product by MTRL: {ProductId}", existingProduct.Id);
            }
        }

        // Step 2: Try to match by CODE (Sku/Barcode) - only in products without InternalId
        if (existingProduct == null && !string.IsNullOrEmpty(code))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p =>
                    string.IsNullOrEmpty(p.InternalId) &&
                    (p.Sku == code || p.Barcode == code),
                    cancellationToken);

            if (existingProduct != null)
            {
                matchType = "CODE";
                _logger.LogDebug("Found existing product by CODE: {ProductId}", existingProduct.Id);
            }
        }

        // Step 3: Try to match by CODE1 (Sku/Barcode) - only in products without InternalId
        if (existingProduct == null && !string.IsNullOrEmpty(code1))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p =>
                    string.IsNullOrEmpty(p.InternalId) &&
                    (p.Sku == code1 || p.Barcode == code1),
                    cancellationToken);

            if (existingProduct != null)
            {
                matchType = "CODE1";
                _logger.LogDebug("Found existing product by CODE1: {ProductId}", existingProduct.Id);
            }
        }

        // Step 4: Update existing or create new
        if (existingProduct != null)
        {
            // Update existing product
            MapSoftOneDataToProduct(softOneProduct, existingProduct);
            existingProduct.LastSyncedAt = DateTime.UtcNow;
            existingProduct.LastSyncStatus = "SoftOne: Updated";
            existingProduct.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated existing product {ProductId} via {MatchType} match",
                existingProduct.Id, matchType);

            return new ProductMatchResult
            {
                Product = existingProduct,
                Action = ProductAction.Updated,
                MatchType = matchType,
                Success = true
            };
        }
        else
        {
            // Create new product
            var newProduct = CreateProductFromSoftOne(softOneProduct);

            _context.Products.Add(newProduct);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new product {ProductId} from SoftOne data", newProduct.Id);

            return new ProductMatchResult
            {
                Product = newProduct,
                Action = ProductAction.Created,
                MatchType = "None",
                Success = true
            };
        }
    }

    /// <summary>
    /// Maps SoftOne product data to an existing Product entity
    /// </summary>
    private void MapSoftOneDataToProduct(Dictionary<string, string?> softOneProduct, Product product)
    {
        // Core identifiers
        product.InternalId = softOneProduct.GetValueOrDefault("ITEM.MTRL") ?? "";
        product.Sku = softOneProduct.GetValueOrDefault("ITEM.CODE") ?? "";
        product.Barcode = softOneProduct.GetValueOrDefault("ITEM.CODE1") ?? "";
        // SoftOne ID είναι το MTRL (InternalId) αν υπάρχει, αλλιώς το SKU
        product.SoftOneId = !string.IsNullOrEmpty(product.InternalId) ? product.InternalId : product.Sku;

        // Product information
        product.Name = softOneProduct.GetValueOrDefault("ITEM.NAME") ?? "";
        product.Category = softOneProduct.GetValueOrDefault("ITEM.MTRCATEGORY") ?? "";
        product.Unit = softOneProduct.GetValueOrDefault("ITEM.MTRUNIT1") ?? "";
        product.Group = softOneProduct.GetValueOrDefault("ITEM.MTRGROUP") ?? "";
        product.Vat = softOneProduct.GetValueOrDefault("ITEM.VAT") ?? "";

        // Pricing - handle decimal parsing safely (SoftOne API returns numbers with period separator)
        if (decimal.TryParse(softOneProduct.GetValueOrDefault("ITEM.PRICER"), NumberStyles.Number, CultureInfo.InvariantCulture, out var retailPrice))
            product.Price = retailPrice;

        if (decimal.TryParse(softOneProduct.GetValueOrDefault("ITEM.PRICEW"), NumberStyles.Number, CultureInfo.InvariantCulture, out var wholesalePrice))
            product.WholesalePrice = wholesalePrice;

        if (decimal.TryParse(softOneProduct.GetValueOrDefault("ITEM.MTRL_ITEMTRDATA_SALLPRICE"), NumberStyles.Number, CultureInfo.InvariantCulture, out var salePrice))
            product.SalePrice = salePrice;

        if (decimal.TryParse(softOneProduct.GetValueOrDefault("ITEM.MTRL_ITEMTRDATA_PURLPRICE"), NumberStyles.Number, CultureInfo.InvariantCulture, out var purchasePrice))
            product.PurchasePrice = purchasePrice;

        if (decimal.TryParse(softOneProduct.GetValueOrDefault("ITEM.SODISCOUNT"), NumberStyles.Number, CultureInfo.InvariantCulture, out var discount))
            product.Discount = discount;

        // Inventory
        if (decimal.TryParse(softOneProduct.GetValueOrDefault("ITEM.MTRL_ITEMTRDATA_QTY1"), NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
            product.Quantity = quantity;

        // Additional data
        product.ImageData = softOneProduct.GetValueOrDefault("ITEM.MTRL_ITEDOCDATA_SODATA") ?? "";
        product.ZoomInfo = softOneProduct.GetValueOrDefault("ZOOMINFO") ?? "";
    }

    /// <summary>
    /// Creates a new Product entity from SoftOne data
    /// </summary>
    private Product CreateProductFromSoftOne(Dictionary<string, string?> softOneProduct)
    {
        var product = new Product
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow,
            LastSyncStatus = "SoftOne: Created"
        };

        MapSoftOneDataToProduct(softOneProduct, product);
        return product;
    }

    /// <summary>
    /// Processes an ATUM inventory item using the 2-level matching strategy:
    /// 1. Match by ATUM ID - highest priority
    /// 2. Match by SKU - fallback
    /// 3. Create new if no match found
    /// </summary>
    public async Task<ProductMatchResult> ProcessAtumProductAsync(
        AtumInventoryItem atumItem,
        CancellationToken cancellationToken = default)
    {
        var atumId = atumItem.Id.ToString();
        var sku = atumItem.GetSku();

        _logger.LogDebug("Processing ATUM product: AtumId={AtumId}, SKU={SKU}, Quantity={Quantity}",
            atumId, sku, atumItem.GetStockQuantity());

        Product? existingProduct = null;
        string matchType = "";

        // Step 1: Try to match by ATUM ID - highest priority
        if (!string.IsNullOrEmpty(atumId))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.AtumId == atumId, cancellationToken);

            if (existingProduct != null)
            {
                matchType = "AtumId";
                _logger.LogDebug("Found existing product by ATUM ID: {ProductId}", existingProduct.Id);
            }
        }

        // Step 2: Try to match by SKU - fallback
        if (existingProduct == null && !string.IsNullOrEmpty(sku))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);

            if (existingProduct != null)
            {
                matchType = "SKU";
                _logger.LogDebug("Found existing product by SKU: {ProductId}", existingProduct.Id);
            }
        }

        // Step 3: Update existing or create new
        if (existingProduct != null)
        {
            // Check if SKU would change and if it would conflict
            var atumSku = atumItem.GetSku();
            if (!string.IsNullOrEmpty(atumSku) &&
                existingProduct.Sku != atumSku &&
                !string.IsNullOrEmpty(existingProduct.Sku))
            {
                // SKU would change - check if new SKU already exists in another product
                var skuConflict = await _context.Products
                    .AnyAsync(p => p.Id != existingProduct.Id && p.Sku == atumSku, cancellationToken);

                if (skuConflict)
                {
                    _logger.LogWarning("Cannot update product {ProductId} SKU from '{OldSku}' to '{NewSku}' - SKU already exists",
                        existingProduct.Id, existingProduct.Sku, atumSku);

                    // Just update quantities without changing SKU
                    existingProduct.AtumId = atumItem.Id.ToString();
                    existingProduct.AtumQuantity = atumItem.GetStockQuantity();
                    existingProduct.WooCommerceId = atumItem.ProductId.ToString();
                    existingProduct.LastSyncedAt = DateTime.UtcNow;
                    existingProduct.LastSyncStatus = "ATUM: Partial Update (SKU conflict)";
                    existingProduct.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Safe to update including SKU
                    await MapAtumDataToProductAsync(atumItem, existingProduct, cancellationToken);
                    existingProduct.LastSyncedAt = DateTime.UtcNow;
                    existingProduct.LastSyncStatus = "ATUM: Updated";
                    existingProduct.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // No SKU conflict - update normally
                await MapAtumDataToProductAsync(atumItem, existingProduct, cancellationToken);
                existingProduct.LastSyncedAt = DateTime.UtcNow;
                existingProduct.LastSyncStatus = "ATUM: Updated";
                existingProduct.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Updated existing product {ProductId} with ATUM data via {MatchType} match",
                    existingProduct.Id, matchType);

                return new ProductMatchResult
                {
                    Product = existingProduct,
                    Action = ProductAction.Updated,
                    MatchType = matchType,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save updates for product {ProductId} (SKU: {Sku})",
                    existingProduct.Id, existingProduct.Sku);

                // Clear any failed changes from the ChangeTracker to prevent issues with subsequent saves
                _context.ChangeTracker.Clear();

                return new ProductMatchResult
                {
                    Product = existingProduct,
                    Action = ProductAction.Updated,
                    MatchType = matchType,
                    Success = false,
                    ErrorMessage = $"Save failed: {ex.Message}"
                };
            }
        }
        else
        {
            // Check if a product with this SKU already exists before creating
            if (!string.IsNullOrEmpty(sku))
            {
                var skuExists = await _context.Products.AnyAsync(p => p.Sku == sku, cancellationToken);
                if (skuExists)
                {
                    _logger.LogWarning("Cannot create product from ATUM item {AtumId} - SKU '{Sku}' already exists",
                        atumItem.Id, sku);

                    return new ProductMatchResult
                    {
                        Product = null!,
                        Action = ProductAction.Skipped,
                        MatchType = "None",
                        Success = false,
                        ErrorMessage = $"SKU '{sku}' already exists"
                    };
                }
            }

            try
            {
                // Create new product from ATUM data
                var newProduct = await CreateProductFromAtumAsync(atumItem, cancellationToken);

                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Created new product {ProductId} from ATUM data", newProduct.Id);

                return new ProductMatchResult
                {
                    Product = newProduct,
                    Action = ProductAction.Created,
                    MatchType = "None",
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create product from ATUM item {AtumId} (SKU: {Sku})",
                    atumItem.Id, sku);

                // Clear any failed changes from the ChangeTracker to prevent issues with subsequent saves
                _context.ChangeTracker.Clear();

                return new ProductMatchResult
                {
                    Product = null!,
                    Action = ProductAction.Created,
                    MatchType = "None",
                    Success = false,
                    ErrorMessage = $"Create failed: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// Maps ATUM inventory data to an existing Product entity
    /// </summary>
    private async Task MapAtumDataToProductAsync(AtumInventoryItem atumItem, Product product, CancellationToken cancellationToken = default)
    {
        // Update ATUM specific fields
        product.AtumId = atumItem.Id.ToString();
        product.AtumQuantity = atumItem.GetStockQuantity();

        // Update WooCommerce ID - το ProductId από ATUM είναι το WooCommerce ID
        product.WooCommerceId = atumItem.ProductId.ToString();

        // Update basic fields if not already set or if ATUM has newer data
        if (string.IsNullOrEmpty(product.Name) && !string.IsNullOrEmpty(atumItem.Name))
            product.Name = atumItem.Name;

        // Update SKU only if product doesn't have one AND the new SKU doesn't conflict
        if (string.IsNullOrEmpty(product.Sku) && !string.IsNullOrEmpty(atumItem.GetSku()))
        {
            var atumSku = atumItem.GetSku();
            var skuExists = await _context.Products.AnyAsync(p => p.Id != product.Id && p.Sku == atumSku, cancellationToken);

            if (!skuExists)
            {
                product.Sku = atumSku;
            }
            else
            {
                _logger.LogWarning("Cannot set SKU '{Sku}' for product {ProductId} - SKU already exists in another product",
                    atumSku, product.Id);
            }
        }
    }

    /// <summary>
    /// Creates a new Product entity from ATUM data and fetches additional info from WooCommerce if needed
    /// </summary>
    private async Task<Product> CreateProductFromAtumAsync(AtumInventoryItem atumItem, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            AtumId = atumItem.Id.ToString(),
            WooCommerceId = atumItem.ProductId.ToString(), // Store WooCommerce ID από ATUM
            SoftOneId = atumItem.GetSku(), // Χρησιμοποιούμε το SKU ως SoftOneId όταν δεν υπάρχει MTRL
            Sku = atumItem.GetSku(),
            Name = atumItem.Name,
            AtumQuantity = atumItem.GetStockQuantity(),
            Quantity = 0, // SoftOne quantity starts at 0
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow,
            LastSyncStatus = "ATUM: Created"
        };

        // If product name is missing and we have a WooCommerce ID, try to fetch it from WooCommerce
        _logger.LogInformation("ATUM Product Debug: Name='{Name}', ProductId={ProductId}", product.Name, atumItem.ProductId);

        if (string.IsNullOrEmpty(product.Name) && atumItem.ProductId > 0)
        {
            try
            {
                _logger.LogInformation("Product name missing for ATUM item {AtumId}, fetching from WooCommerce product {WooId}",
                    atumItem.Id, atumItem.ProductId);

                // Get settings to fetch WooCommerce product info
                var settings = await _context.AppSettings.FirstOrDefaultAsync(cancellationToken);
                _logger.LogInformation("WooCommerce settings check: Url='{Url}', HasKey={HasKey}",
                    settings?.WooCommerceUrl, !string.IsNullOrEmpty(settings?.WooCommerceConsumerKey));

                if (settings != null && !string.IsNullOrEmpty(settings.WooCommerceUrl) && !string.IsNullOrEmpty(settings.WooCommerceConsumerKey))
                {
                    _logger.LogInformation("Calling WooCommerce API for product {ProductId}", atumItem.ProductId);
                    var wooProducts = await _wooCommerceApiService.GetProductsByIdsAsync(
                        settings.WooCommerceConsumerKey,
                        settings.WooCommerceConsumerSecret,
                        new List<int> { atumItem.ProductId },
                        cancellationToken);

                    _logger.LogInformation("WooCommerce API returned {Count} products", wooProducts?.Count ?? 0);

                    var wooProduct = wooProducts?.FirstOrDefault();
                    if (wooProduct != null && !string.IsNullOrEmpty(wooProduct.Name))
                    {
                        product.Name = wooProduct.Name;
                        _logger.LogInformation("Fetched product name '{Name}' from WooCommerce for ATUM item {AtumId}",
                            wooProduct.Name, atumItem.Id);

                        // Also update price if available
                        if (product.Price == 0 && !string.IsNullOrEmpty(wooProduct.RegularPrice) && decimal.TryParse(wooProduct.RegularPrice, out var price) && price > 0)
                            product.Price = price;
                    }
                    else
                    {
                        _logger.LogWarning("No WooCommerce product found or name is empty for product ID {ProductId}", atumItem.ProductId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch product name from WooCommerce for ATUM item {AtumId}, WooCommerce ID {WooId}",
                    atumItem.Id, atumItem.ProductId);
            }
        }

        return product;
    }

    /// <summary>
    /// Processes a WooCommerce product using the 2-level matching strategy:
    /// 1. Match by WooCommerce ID - highest priority
    /// 2. Match by SKU - fallback
    /// 3. Update existing or skip if no match found (don't create new from WooCommerce alone)
    /// </summary>
    public async Task<ProductMatchResult> ProcessWooCommerceProductAsync(
        WooCommerceProduct wooProduct,
        CancellationToken cancellationToken = default)
    {
        var wooId = wooProduct.Id.ToString();
        var sku = wooProduct.Sku;

        _logger.LogDebug("Processing WooCommerce product: WooId={WooId}, SKU={SKU}, Name={Name}",
            wooId, sku, wooProduct.Name);

        Product? existingProduct = null;
        string matchType = "";

        // Step 1: Try to match by WooCommerce ID - highest priority
        if (!string.IsNullOrEmpty(wooId))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.WooCommerceId == wooId, cancellationToken);

            if (existingProduct != null)
            {
                matchType = "WooCommerceId";
                _logger.LogDebug("Found existing product by WooCommerce ID: {ProductId}", existingProduct.Id);
            }
        }

        // Step 2: Try to match by SKU - fallback
        if (existingProduct == null && !string.IsNullOrEmpty(sku))
        {
            existingProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);

            if (existingProduct != null)
            {
                matchType = "SKU";
                _logger.LogDebug("Found existing product by SKU: {ProductId}", existingProduct.Id);
            }
        }

        // Step 3: Update existing product with WooCommerce data (don't create new from WooCommerce alone)
        if (existingProduct != null)
        {
            // Update existing product with WooCommerce data
            MapWooCommerceDataToProduct(wooProduct, existingProduct);
            existingProduct.LastSyncedAt = DateTime.UtcNow;
            existingProduct.LastSyncStatus = "WooCommerce: Updated";
            existingProduct.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated existing product {ProductId} with WooCommerce data via {MatchType} match",
                existingProduct.Id, matchType);

            return new ProductMatchResult
            {
                Product = existingProduct,
                Action = ProductAction.Updated,
                MatchType = matchType,
                Success = true
            };
        }
        else
        {
            // No matching product found - skip (don't create new products from WooCommerce alone)
            _logger.LogDebug("No matching product found for WooCommerce product {WooId} - skipping", wooId);

            return new ProductMatchResult
            {
                Product = null!,
                Action = ProductAction.Skipped,
                MatchType = "None",
                Success = true,
                ErrorMessage = $"No matching product found for WooCommerce ID {wooId}"
            };
        }
    }

    /// <summary>
    /// Maps WooCommerce product data to an existing Product entity
    /// </summary>
    private void MapWooCommerceDataToProduct(WooCommerceProduct wooProduct, Product product)
    {
        // Update WooCommerce specific fields
        product.WooCommerceId = wooProduct.Id.ToString();

        // Update basic fields if not already set or if WooCommerce has newer/better data
        if (string.IsNullOrEmpty(product.Name) && !string.IsNullOrEmpty(wooProduct.Name))
            product.Name = wooProduct.Name;

        if (string.IsNullOrEmpty(product.Sku) && !string.IsNullOrEmpty(wooProduct.Sku))
            product.Sku = wooProduct.Sku;

        // Update pricing information
        if (product.Price == 0 && wooProduct.RegularPrice > 0)
            product.Price = wooProduct.RegularPrice;

        if (!product.SalePrice.HasValue && wooProduct.SalePrice > 0)
            product.SalePrice = wooProduct.SalePrice;
    }

    /// <summary>
    /// Gets products that have missing names and could benefit from WooCommerce data
    /// </summary>
    public async Task<List<Product>> GetProductsMissingNamesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.Sku))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets distinct WooCommerce product IDs from products with missing names
    /// </summary>
    public async Task<List<int>> GetWooCommerceIdsForProductsWithMissingNamesAsync(CancellationToken cancellationToken = default)
    {
        var productIds = await _context.Products
            .Where(p => string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.WooCommerceId))
            .Select(p => p.WooCommerceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return productIds
            .Where(id => int.TryParse(id, out _))
            .Select(id => int.Parse(id))
            .ToList();
    }

}

/// <summary>
/// Result of processing a SoftOne product
/// </summary>
public class ProductMatchResult
{
    public Product Product { get; set; } = null!;
    public ProductAction Action { get; set; }
    public string MatchType { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Actions performed on products during sync
/// </summary>
public enum ProductAction
{
    Created,
    Updated,
    Skipped,
    Error
}