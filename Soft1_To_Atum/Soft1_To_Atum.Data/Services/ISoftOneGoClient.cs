namespace Soft1_To_Atum.Data.Services;

public interface ISoftOneGoClient
{
    Task<List<SoftOneProduct>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<SoftOneProduct?> GetProductByIdAsync(string productId, CancellationToken cancellationToken = default);
}

public class SoftOneProduct
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}