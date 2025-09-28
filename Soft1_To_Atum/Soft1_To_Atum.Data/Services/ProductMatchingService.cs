using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;
using System.Globalization;

namespace Soft1_To_Atum.Data.Services;

public class ProductMatchingService
{
    private readonly SyncDbContext _context;
    private readonly ILogger<ProductMatchingService> _logger;

    public ProductMatchingService(SyncDbContext context, ILogger<ProductMatchingService> logger)
    {
        _context = context;
        _logger = logger;
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
            existingProduct.LastSyncStatus = "Updated";
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
        product.SoftOneId = product.Sku; // Keep legacy field populated

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
            LastSyncStatus = "Created"
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
            // Update existing product with ATUM data
            MapAtumDataToProduct(atumItem, existingProduct);
            existingProduct.LastSyncedAt = DateTime.UtcNow;
            existingProduct.LastSyncStatus = "Updated";
            existingProduct.UpdatedAt = DateTime.UtcNow;

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
        else
        {
            // Create new product from ATUM data
            var newProduct = CreateProductFromAtum(atumItem);

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
    }

    /// <summary>
    /// Maps ATUM inventory data to an existing Product entity
    /// </summary>
    private void MapAtumDataToProduct(AtumInventoryItem atumItem, Product product)
    {
        // Update ATUM specific fields
        product.AtumId = atumItem.Id.ToString();
        product.AtumQuantity = atumItem.GetStockQuantity();

        // Update basic fields if not already set or if ATUM has newer data
        if (string.IsNullOrEmpty(product.Name) && !string.IsNullOrEmpty(atumItem.Name))
            product.Name = atumItem.Name;

        if (string.IsNullOrEmpty(product.Sku) && !string.IsNullOrEmpty(atumItem.GetSku()))
            product.Sku = atumItem.GetSku();
    }

    /// <summary>
    /// Creates a new Product entity from ATUM data
    /// </summary>
    private Product CreateProductFromAtum(AtumInventoryItem atumItem)
    {
        var product = new Product
        {
            AtumId = atumItem.Id.ToString(),
            Sku = atumItem.GetSku(),
            Name = atumItem.Name,
            AtumQuantity = atumItem.GetStockQuantity(),
            Quantity = 0, // SoftOne quantity starts at 0
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow,
            LastSyncStatus = "Created"
        };

        return product;
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