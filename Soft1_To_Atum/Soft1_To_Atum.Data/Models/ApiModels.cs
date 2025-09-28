using System.Text.Json.Serialization;
using System.Globalization;

namespace Soft1_To_Atum.Data.Models;

// Response models για τα API endpoints
public class SyncStatusResponse
{
    public bool IsRunning { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncDuration { get; set; }
    public int TotalProducts { get; set; }
    public SyncStatsResponse? LastSyncStats { get; set; }
}

public class SyncStatsResponse
{
    public int Total { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
}

public class SyncLogResponse
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalProducts { get; set; }
    public int CreatedProducts { get; set; }
    public int UpdatedProducts { get; set; }
    public int SkippedProducts { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
}

public class ProductResponse
{
    public int Id { get; set; }

    // External System IDs
    public string SoftOneId { get; set; } = string.Empty;
    public string WooCommerceId { get; set; } = string.Empty;
    public string AtumId { get; set; } = string.Empty;

    // SoftOne Fields for Matching
    public string InternalId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    // Product Information
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Vat { get; set; } = string.Empty;

    // Pricing
    public decimal Price { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? Discount { get; set; }

    // Inventory
    public decimal Quantity { get; set; }

    // Additional Data
    public string ImageData { get; set; } = string.Empty;
    public string ZoomInfo { get; set; } = string.Empty;

    // Sync Tracking
    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string LastSyncStatus { get; set; } = string.Empty;
    public string? LastSyncError { get; set; }
}

public class ProductsPageResponse
{
    public List<ProductResponse> Products { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

public class StoreResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WooCommerceUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ManualSyncResponse
{
    public string Message { get; set; } = string.Empty;
    public int SyncLogId { get; set; }
}

public class ConnectionTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

// API Models για Settings
public class ApiSettingsModel
{
    public string Name { get; set; } = "Κατάστημα Κέντρο";
    public bool Enabled { get; set; } = true;
    public SoftOneGoSettings SoftOneGo { get; set; } = new();
    public WooCommerceSettings WooCommerce { get; set; } = new();
    public AtumSettings ATUM { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public SyncSettings Sync { get; set; } = new();
    public MatchingSettings Matching { get; set; } = new();
    public FieldMappingSettings FieldMapping { get; set; } = new();
}

public class SoftOneGoSettings
{
    public string BaseUrl { get; set; } = "https://go.s1cloud.net/s1services";
    public string AppId { get; set; } = "703";
    public string Token { get; set; } = string.Empty;
    public string S1Code { get; set; } = string.Empty;
    public string Filters { get; set; } = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999";
}

public class WooCommerceSettings
{
    public string Url { get; set; } = string.Empty;
    public string ConsumerKey { get; set; } = string.Empty;
    public string ConsumerSecret { get; set; } = string.Empty;
    public string Version { get; set; } = "wc/v3";
}

public class AtumSettings
{
    public int LocationId { get; set; } = 870;
    public string LocationName { get; set; } = "store1_location";
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
}

public class SyncSettings
{
    public int IntervalMinutes { get; set; } = 15;
    public bool AutoSync { get; set; } = true;
    public bool EmailNotifications { get; set; } = true;
}

public class MatchingSettings
{
    public string PrimaryField { get; set; } = "sku";
    public string SecondaryField { get; set; } = "barcode";
    public bool CreateMissingProducts { get; set; } = true;
    public bool UpdateExistingProducts { get; set; } = true;
}

public class FieldMappingSettings
{
    public string Sku { get; set; } = "ITEM.CODE1";
    public string Name { get; set; } = "ITEM.NAME";
    public string Price { get; set; } = "ITEM.PRICER";
    public string StockQuantity { get; set; } = "ITEM.MTRL_ITEMTRDATA_QTY1";
    public string Category { get; set; } = "ITEM.MTRCATEGORY";
    public string Unit { get; set; } = "ITEM.MTRUNIT1";
    public string Vat { get; set; } = "ITEM.VAT";
}

// SoftOne API Models
public class SoftOneApiResponse
{
    /// <summary>
    /// Indicates if the API call was successful
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    /// <summary>
    /// Update timestamp from SoftOne API in format "YYYYMMDD HH:mm:ss"
    /// </summary>
    [JsonPropertyName("upddate")]
    public string UpdateDate { get; set; } = string.Empty;
    
    /// <summary>
    /// Request ID from SoftOne API
    /// </summary>
    [JsonPropertyName("reqID")]
    public string RequestId { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of records returned
    /// </summary>
    [JsonPropertyName("totalcount")]
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Field definitions describing the structure of each row
    /// </summary>
    [JsonPropertyName("fields")]
    public List<SoftOneFieldDefinition> Fields { get; set; } = [];
    
    /// <summary>
    /// Data rows - each row corresponds to one product with values matching the Fields order
    /// </summary>
    [JsonPropertyName("rows")]
    public List<List<string?>> Rows { get; set; } = [];
    
    /// <summary>
    /// Converts the raw API response to a list of structured SoftOneProduct objects
    /// </summary>
    public List<SoftOneProduct> GetProducts()
    {
        return Rows.Select(row => SoftOneProduct.FromApiRow(row, Fields)).ToList();
    }
}

public class SoftOneFieldDefinition
{
    /// <summary>
    /// Field name (e.g., "ITEM.CODE", "ITEM.NAME", etc.)
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Field data type (e.g., "string", "float", "image")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class SoftOneProduct
{
    public string ZoomInfo { get; set; } = string.Empty;
    public string ImageData { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? WholesalePrice { get; set; }
    public decimal? RetailPrice { get; set; }
    public string Group { get; set; } = string.Empty;
    public decimal? StockQuantity { get; set; }
    public string Vat { get; set; } = string.Empty;
    public decimal? Discount { get; set; }
    public string InternalId { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal? SalePrice { get; set; }
    public decimal? PurchasePrice { get; set; }

    // Factory method to create from raw SoftOne API row data
    public static SoftOneProduct FromApiRow(List<string?> row, List<SoftOneFieldDefinition> fields)
    {
        var product = new SoftOneProduct();

        for (int i = 0; i < fields.Count && i < row.Count; i++)
        {
            var fieldName = fields[i].Name;
            var value = row[i];

            switch (fieldName)
            {
                case "ZOOMINFO":
                    product.ZoomInfo = value ?? string.Empty;
                    break;
                case "ITEM.MTRL_ITEDOCDATA_SODATA":
                    product.ImageData = value ?? string.Empty;
                    break;
                case "ITEM.CODE":
                    product.Code = value ?? string.Empty;
                    break;
                case "ITEM.NAME":
                    product.Name = value ?? string.Empty;
                    break;
                case "ITEM.MTRCATEGORY":
                    product.Category = value ?? string.Empty;
                    break;
                case "ITEM.MTRUNIT1":
                    product.Unit = value ?? string.Empty;
                    break;
                case "ITEM.PRICEW":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var wholesale))
                        product.WholesalePrice = wholesale;
                    break;
                case "ITEM.PRICER":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var retail))
                        product.RetailPrice = retail;
                    break;
                case "ITEM.MTRGROUP":
                    product.Group = value ?? string.Empty;
                    break;
                case "ITEM.MTRL_ITEMTRDATA_QTY1":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var stock))
                        product.StockQuantity = stock;
                    break;
                case "ITEM.VAT":
                    product.Vat = value ?? string.Empty;
                    break;
                case "ITEM.SODISCOUNT":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var discount))
                        product.Discount = discount;
                    break;
                case "ITEM.MTRL":
                    product.InternalId = value ?? string.Empty;
                    break;
                case "ITEM.CODE1":
                    product.Barcode = value ?? string.Empty;
                    break;
                case "ITEM.MTRL_ITEMTRDATA_SALLPRICE":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var salePrice))
                        product.SalePrice = salePrice;
                    break;
                case "ITEM.MTRL_ITEMTRDATA_PURLPRICE":
                    if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var purchasePrice))
                        product.PurchasePrice = purchasePrice;
                    break;
            }
        }

        return product;
    }

}