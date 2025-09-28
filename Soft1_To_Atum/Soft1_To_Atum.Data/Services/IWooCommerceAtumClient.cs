namespace Soft1_To_Atum.Data.Services;

public interface IWooCommerceAtumClient
{
    Task<List<WooCommerceProduct>> GetProductsAsync(int storeId, CancellationToken cancellationToken = default);
    Task<WooCommerceProduct?> GetProductBySkuAsync(int storeId, string sku, CancellationToken cancellationToken = default);
    Task<WooCommerceProduct> CreateProductAsync(int storeId, CreateWooCommerceProductRequest request, CancellationToken cancellationToken = default);
    Task<WooCommerceProduct> UpdateProductAsync(int storeId, int productId, UpdateWooCommerceProductRequest request, CancellationToken cancellationToken = default);
    Task UpdateAtumInventoryAsync(int storeId, int productId, UpdateAtumInventoryRequest request, CancellationToken cancellationToken = default);
}

public class WooCommerceProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal RegularPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public bool ManageStock { get; set; }
    public string StockStatus { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
    public AtumInventory? AtumInventory { get; set; }
}

public class AtumInventory
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class CreateWooCommerceProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal RegularPrice { get; set; }
    public string Description { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public bool ManageStock { get; set; } = true;
}

public class UpdateWooCommerceProductRequest
{
    public string? Name { get; set; }
    public decimal? RegularPrice { get; set; }
    public string? Description { get; set; }
    public int? StockQuantity { get; set; }
}

public class UpdateAtumInventoryRequest
{
    public int Quantity { get; set; }
    public string Location { get; set; } = string.Empty;
}