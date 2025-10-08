namespace Soft1_To_Atum.Data.Models;

public class AutoSyncLog
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running"; // Running, Completed, Failed
    public int TotalStores { get; set; }
    public int SuccessfulStores { get; set; }
    public int FailedStores { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; } // JSON with details per store
}
