namespace Soft1_To_Atum.Data.Models;

public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WooCommerceUrl { get; set; } = string.Empty;
    public string WooCommerceKey { get; set; } = string.Empty;
    public string WooCommerceSecret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
}