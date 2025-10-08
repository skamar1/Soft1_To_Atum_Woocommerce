namespace Soft1_To_Atum.Data.Models;

public class SyncLog
{
    public int Id { get; set; }

    // Store Association
    public int? StoreSettingsId { get; set; }
    public virtual StoreSettings? StoreSettings { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalProducts { get; set; }
    public int CreatedProducts { get; set; }
    public int UpdatedProducts { get; set; }
    public int SkippedProducts { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = string.Empty; // Running, Completed, Failed
    public string? ErrorDetails { get; set; }
    public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);
}