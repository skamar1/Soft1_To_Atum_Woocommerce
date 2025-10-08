using System.ComponentModel.DataAnnotations;

namespace Soft1_To_Atum.Data.Models;

/// <summary>
/// Store-specific settings - each store has unique SoftOne Go and ATUM configuration
/// </summary>
public class StoreSettings
{
    [Key]
    public int Id { get; set; }

    // Store Information
    public string StoreName { get; set; } = "Κατάστημα Κέντρο";
    public bool StoreEnabled { get; set; } = true;

    // SoftOne Go Settings (unique per store)
    public string SoftOneGoBaseUrl { get; set; } = "https://go.s1cloud.net/s1services";
    public string SoftOneGoAppId { get; set; } = "703";
    public string SoftOneGoToken { get; set; } = string.Empty;
    public string SoftOneGoS1Code { get; set; } = string.Empty;
    public string SoftOneGoFilters { get; set; } = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999";

    // ATUM Multi-Inventory Settings (unique per store)
    public int AtumLocationId { get; set; } = 870;
    public string AtumLocationName { get; set; } = "store1_location";

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<SyncLog> SyncLogs { get; set; } = new List<SyncLog>();
}
