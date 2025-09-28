namespace Soft1_To_Atum.Data.Models;

public class Product
{
    public int Id { get; set; }
    public string SoftOneId { get; set; } = string.Empty;
    public string WooCommerceId { get; set; } = string.Empty;
    public string AtumId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string LastSyncStatus { get; set; } = string.Empty;
    public string? LastSyncError { get; set; }
}