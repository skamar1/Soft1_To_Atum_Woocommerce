namespace Soft1_To_Atum.Data.Models;

public class Product
{
    public int Id { get; set; }

    // External System IDs
    public string SoftOneId { get; set; } = string.Empty; // ITEM.CODE (legacy)
    public string WooCommerceId { get; set; } = string.Empty;
    public string AtumId { get; set; } = string.Empty;

    // SoftOne Fields for Matching
    public string InternalId { get; set; } = string.Empty; // ITEM.MTRL (primary key)
    public string Sku { get; set; } = string.Empty; // ITEM.CODE
    public string Barcode { get; set; } = string.Empty; // ITEM.CODE1

    // Product Information
    public string Name { get; set; } = string.Empty; // ITEM.NAME
    public string Category { get; set; } = string.Empty; // ITEM.MTRCATEGORY
    public string Unit { get; set; } = string.Empty; // ITEM.MTRUNIT1
    public string Group { get; set; } = string.Empty; // ITEM.MTRGROUP
    public string Vat { get; set; } = string.Empty; // ITEM.VAT

    // Pricing
    public decimal Price { get; set; } // ITEM.PRICER (retail price)
    public decimal? WholesalePrice { get; set; } // ITEM.PRICEW
    public decimal? SalePrice { get; set; } // ITEM.MTRL_ITEMTRDATA_SALLPRICE
    public decimal? PurchasePrice { get; set; } // ITEM.MTRL_ITEMTRDATA_PURLPRICE
    public decimal? Discount { get; set; } // ITEM.SODISCOUNT

    // Inventory
    public decimal Quantity { get; set; } // ITEM.MTRL_ITEMTRDATA_QTY1

    // Additional Data
    public string ImageData { get; set; } = string.Empty; // ITEM.MTRL_ITEDOCDATA_SODATA
    public string ZoomInfo { get; set; } = string.Empty; // ZOOMINFO

    // Sync Tracking
    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string LastSyncStatus { get; set; } = string.Empty;
    public string? LastSyncError { get; set; }
}