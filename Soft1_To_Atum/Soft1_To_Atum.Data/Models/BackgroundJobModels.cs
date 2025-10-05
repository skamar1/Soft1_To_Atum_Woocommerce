namespace Soft1_To_Atum.Data.Models;

public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum JobType
{
    WooCommerceSync,
    SoftOneSync,
    AtumSync,
    AtumBatchSync
}

public class BackgroundJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public JobType Type { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Result { get; set; }

    // Progress tracking
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }

    public double ProgressPercentage => TotalSteps > 0 ? (double)CompletedSteps / TotalSteps * 100 : 0;
}

public class JobProgressUpdate
{
    public string JobId { get; set; } = string.Empty;
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WooCommerceSyncJobResult
{
    public int TotalProductsFetched { get; set; }
    public int ProductsMatched { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsSkipped { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime CompletedAt { get; set; }
}

public class AtumBatchPrepareResult
{
    public AtumBatchRequest BatchRequest { get; set; } = new();
    public List<(string Sku, string Name, int WooCommerceId)> CreatedDraftProducts { get; set; } = new();
}