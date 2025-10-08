using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Blazor.Services;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] private SyncApiClient SyncApi { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private ILogger<Home> Logger { get; set; } = null!;

    private List<StoreResponse> stores = new();
    private int? selectedStoreId = null;
    private bool isLoadingStores = false;

    private bool isSyncRunning = false;
    private bool isWooCommerceSyncRunning = false;
    private string? currentWooCommerceJobId = null;
    private System.Threading.Timer? wooCommerceProgressTimer;
    private bool isAtumSyncRunning = false;
    private bool isAtumBatchSyncRunning = false;
    private string lastSyncMessage = string.Empty;
    private Severity lastSyncSeverity = Severity.Info;
    private SyncLogResponse? lastSyncLog;
    private ProductStatisticsResponse? productStatistics;
    private AutoSyncLog? lastAutoSyncLog;

    // WooCommerce page-by-page sync tracking
    private int currentPage = 0;
    private int totalPages = 0;
    private int totalFetched = 0;
    private int totalCreated = 0;
    private int totalSkipped = 0;
    private CancellationTokenSource? pageSyncCancellationTokenSource;

    // ATUM batch-by-batch sync tracking
    private int currentBatch = 0;
    private int totalBatchCreated = 0;
    private int totalBatchUpdated = 0;
    private int totalBatchErrors = 0;
    private CancellationTokenSource? batchSyncCancellationTokenSource;
    private List<DraftProductInfo> allDraftProducts = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadStores();
        await RefreshDashboard();
    }

    private async Task LoadStores()
    {
        isLoadingStores = true;
        try
        {
            var storeResponses = await SyncApi.GetStoresAsync();
            if (storeResponses != null)
            {
                stores = storeResponses.ToList();
                // Select first store by default
                if (stores.Count > 0)
                {
                    selectedStoreId = stores.First().Id;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading stores");
            Snackbar.Add($"Error loading stores: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoadingStores = false;
        }
    }

    private async Task StartManualSync()
    {
        if (isSyncRunning) return;

        if (!selectedStoreId.HasValue)
        {
            Snackbar.Add("Please select a store first", Severity.Warning);
            return;
        }

        Logger.LogInformation("User initiated manual sync from dashboard for store {StoreId}", selectedStoreId.Value);
        isSyncRunning = true;
        lastSyncMessage = "Starting synchronization...";
        lastSyncSeverity = Severity.Info;
        StateHasChanged();

        try
        {
            var result = await SyncApi.StartManualSyncAsync(selectedStoreId.Value);

            if (result != null)
            {
                lastSyncMessage = result.Message;
                lastSyncSeverity = Severity.Success;
                Logger.LogInformation("Manual sync completed successfully. SyncLogId: {SyncLogId}", result.SyncLogId);

                // Refresh dashboard to show updated statistics
                await RefreshDashboard();

                Snackbar.Add("Sync completed successfully!", Severity.Success);
            }
            else
            {
                lastSyncMessage = "Sync completed but no details were returned";
                lastSyncSeverity = Severity.Warning;
                Logger.LogWarning("Manual sync returned null result");

                Snackbar.Add("Sync completed but response was empty", Severity.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            lastSyncMessage = $"Connection error: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "HTTP error during manual sync: {Message}", ex.Message);

            Snackbar.Add("Failed to connect to sync service", Severity.Error);
        }
        catch (Exception ex)
        {
            lastSyncMessage = $"Sync failed: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "Error during manual sync: {Message}", ex.Message);

            Snackbar.Add($"Sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isSyncRunning = false;
            StateHasChanged();
        }
    }

    private async Task StartWooCommerceSync()
    {
        if (isWooCommerceSyncRunning) return;

        Logger.LogInformation("User initiated WooCommerce page-by-page sync from dashboard");
        isWooCommerceSyncRunning = true;

        // Reset tracking variables
        currentPage = 0;
        totalPages = 0;
        totalFetched = 0;
        totalCreated = 0;
        totalSkipped = 0;

        lastSyncMessage = "Starting WooCommerce page-by-page synchronization...";
        lastSyncSeverity = Severity.Info;

        // Create cancellation token source
        pageSyncCancellationTokenSource = new CancellationTokenSource();

        StateHasChanged();

        try
        {
            int page = 1;
            bool hasMore = true;

            while (hasMore && !pageSyncCancellationTokenSource.Token.IsCancellationRequested)
            {
                currentPage = page;
                lastSyncMessage = $"Fetching page {page}...";
                StateHasChanged();

                Logger.LogInformation("Fetching WooCommerce page {Page}", page);

                var result = await SyncApi.SyncWooCommercePageAsync(page);

                if (result != null && result.Success)
                {
                    totalFetched += result.ProductsFetched;
                    totalCreated += result.ProductsCreated;
                    totalSkipped += result.ProductsSkipped;
                    hasMore = result.HasMore;

                    lastSyncMessage = $"Page {page}: Fetched {result.ProductsFetched}, Created {result.ProductsCreated}, Skipped {result.ProductsSkipped}";
                    Logger.LogInformation("Page {Page} completed: Fetched={Fetched}, Created={Created}, Skipped={Skipped}, HasMore={HasMore}",
                        page, result.ProductsFetched, result.ProductsCreated, result.ProductsSkipped, result.HasMore);

                    StateHasChanged();

                    // Add a small delay between pages to avoid overwhelming the API
                    if (hasMore)
                    {
                        await Task.Delay(1000, pageSyncCancellationTokenSource.Token);
                    }

                    page++;
                }
                else
                {
                    lastSyncMessage = result?.Message ?? "Unknown error occurred";
                    lastSyncSeverity = Severity.Error;
                    Logger.LogError("WooCommerce page sync failed for page {Page}: {Message}", page, lastSyncMessage);
                    Snackbar.Add($"Page {page} sync failed: {lastSyncMessage}", Severity.Error);
                    break;
                }
            }

            if (pageSyncCancellationTokenSource.Token.IsCancellationRequested)
            {
                lastSyncMessage = $"WooCommerce sync cancelled. Total: Fetched {totalFetched}, Created {totalCreated}, Skipped {totalSkipped}";
                lastSyncSeverity = Severity.Warning;
                Snackbar.Add("WooCommerce sync cancelled", Severity.Warning);
            }
            else
            {
                lastSyncMessage = $"WooCommerce sync completed! Total: Fetched {totalFetched}, Created {totalCreated}, Skipped {totalSkipped}";
                lastSyncSeverity = Severity.Success;
                Logger.LogInformation("WooCommerce page-by-page sync completed. Total fetched={Fetched}, Created={Created}, Skipped={Skipped}",
                    totalFetched, totalCreated, totalSkipped);
                Snackbar.Add("WooCommerce sync completed successfully!", Severity.Success);

                // Refresh dashboard to show updated statistics
                await RefreshDashboard();
            }
        }
        catch (OperationCanceledException)
        {
            lastSyncMessage = $"WooCommerce sync cancelled. Total: Fetched {totalFetched}, Created {totalCreated}, Skipped {totalSkipped}";
            lastSyncSeverity = Severity.Warning;
            Snackbar.Add("WooCommerce sync cancelled", Severity.Warning);
        }
        catch (HttpRequestException ex)
        {
            lastSyncMessage = $"Connection error: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "HTTP error during WooCommerce page sync: {Message}", ex.Message);
            Snackbar.Add("Failed to connect to sync service", Severity.Error);
        }
        catch (Exception ex)
        {
            lastSyncMessage = $"WooCommerce sync failed: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "Error during WooCommerce page sync: {Message}", ex.Message);
            Snackbar.Add($"WooCommerce sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isWooCommerceSyncRunning = false;
            pageSyncCancellationTokenSource?.Dispose();
            pageSyncCancellationTokenSource = null;
            StateHasChanged();
        }
    }

    private void CancelWooCommerceSync()
    {
        if (pageSyncCancellationTokenSource != null && !pageSyncCancellationTokenSource.IsCancellationRequested)
        {
            Logger.LogInformation("User cancelled WooCommerce page-by-page sync");
            pageSyncCancellationTokenSource.Cancel();
        }
    }

    private void StartWooCommerceProgressPolling()
    {
        if (string.IsNullOrEmpty(currentWooCommerceJobId)) return;

        // Poll every 2 seconds for progress updates
        wooCommerceProgressTimer = new System.Threading.Timer(async _ =>
        {
            await CheckWooCommerceJobProgress();
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private async Task CheckWooCommerceJobProgress()
    {
        if (string.IsNullOrEmpty(currentWooCommerceJobId)) return;

        try
        {
            var status = await SyncApi.GetBackgroundJobStatusAsync(currentWooCommerceJobId);

            if (status != null)
            {
                await InvokeAsync(() =>
                {
                    lastSyncMessage = $"{status.CurrentStep} ({status.ProgressPercentage:F1}%)";

                    switch (status.Status.ToLower())
                    {
                        case "completed":
                            lastSyncMessage = "WooCommerce sync completed successfully!";
                            lastSyncSeverity = Severity.Success;
                            isWooCommerceSyncRunning = false;
                            currentWooCommerceJobId = null;
                            wooCommerceProgressTimer?.Dispose();
                            wooCommerceProgressTimer = null;
                            Snackbar.Add("WooCommerce sync completed successfully!", Severity.Success);

                            // Refresh dashboard to show updated statistics
                            _ = Task.Run(RefreshDashboard);
                            break;

                        case "failed":
                            lastSyncMessage = $"WooCommerce sync failed: {status.ErrorMessage}";
                            lastSyncSeverity = Severity.Error;
                            isWooCommerceSyncRunning = false;
                            currentWooCommerceJobId = null;
                            wooCommerceProgressTimer?.Dispose();
                            wooCommerceProgressTimer = null;
                            Snackbar.Add("WooCommerce sync failed", Severity.Error);
                            break;

                        case "cancelled":
                            lastSyncMessage = "WooCommerce sync was cancelled";
                            lastSyncSeverity = Severity.Warning;
                            isWooCommerceSyncRunning = false;
                            currentWooCommerceJobId = null;
                            wooCommerceProgressTimer?.Dispose();
                            wooCommerceProgressTimer = null;
                            Snackbar.Add("WooCommerce sync was cancelled", Severity.Warning);
                            break;

                        default:
                            lastSyncSeverity = Severity.Info;
                            break;
                    }

                    StateHasChanged();
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking WooCommerce job progress for job {JobId}", currentWooCommerceJobId);
        }
    }

    private async Task StartAtumSync()
    {
        if (isAtumSyncRunning) return;

        if (!selectedStoreId.HasValue)
        {
            Snackbar.Add("Please select a store first", Severity.Warning);
            return;
        }

        Logger.LogInformation("User initiated ATUM sync from dashboard for store {StoreId}", selectedStoreId.Value);
        isAtumSyncRunning = true;
        lastSyncMessage = "Starting ATUM synchronization...";
        lastSyncSeverity = Severity.Info;
        StateHasChanged();

        try
        {
            var result = await SyncApi.StartAtumSyncAsync(selectedStoreId.Value);

            if (result != null)
            {
                lastSyncMessage = result.Message;
                lastSyncSeverity = Severity.Success;
                Logger.LogInformation("ATUM sync completed successfully. SyncLogId: {SyncLogId}", result.SyncLogId);

                // Refresh dashboard to show updated statistics
                await RefreshDashboard();

                Snackbar.Add("ATUM sync completed successfully!", Severity.Success);
            }
            else
            {
                lastSyncMessage = "ATUM sync completed but no details were returned";
                lastSyncSeverity = Severity.Warning;
                Logger.LogWarning("ATUM sync returned null result");

                Snackbar.Add("ATUM sync completed but response was empty", Severity.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            lastSyncMessage = $"Connection error: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "HTTP error during ATUM sync: {Message}", ex.Message);

            Snackbar.Add("Failed to connect to sync service", Severity.Error);
        }
        catch (Exception ex)
        {
            lastSyncMessage = $"ATUM sync failed: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "Error during ATUM sync: {Message}", ex.Message);

            Snackbar.Add($"ATUM sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isAtumSyncRunning = false;
            StateHasChanged();
        }
    }

    private async Task StartAtumBatchSync()
    {
        if (isAtumBatchSyncRunning) return;

        Logger.LogInformation("User initiated full sync (WooCommerce + ATUM Create + ATUM Update) from dashboard");
        isAtumBatchSyncRunning = true;

        lastSyncMessage = "Starting comprehensive sync (WooCommerce matching, ATUM creation, ATUM update)...";
        lastSyncSeverity = Severity.Info;

        StateHasChanged();

        try
        {
            var result = await SyncApi.FullSyncAsync();

            if (result != null && result.Success)
            {
                lastSyncMessage = result.Message;
                lastSyncSeverity = Severity.Success;

                Logger.LogInformation("Full sync completed successfully. " +
                    "Total={Total}, WooCommerce: Matched={Matched}, Created={WooCreated}, " +
                    "ATUM: Created={AtumCreated}, Updated={AtumUpdated}, " +
                    "Errors: WooCommerce={Errors}, ATUM Creation={AtumErrors}, ATUM Update={AtumUpdateErrors}",
                    result.TotalProducts, result.MatchedInWooCommerce, result.CreatedInWooCommerce,
                    result.CreatedInAtum, result.UpdatedInAtum,
                    result.Errors, result.AtumErrors, result.AtumUpdateErrors);

                Snackbar.Add($"Sync completed! WooCommerce: {result.CreatedInWooCommerce} created, ATUM: {result.CreatedInAtum} created, {result.UpdatedInAtum} updated", Severity.Success);

                // Refresh dashboard to show updated statistics
                await RefreshDashboard();
            }
            else
            {
                lastSyncMessage = result?.Message ?? "Full sync completed but no details were returned";
                lastSyncSeverity = Severity.Warning;
                Logger.LogWarning("Full sync returned unexpected result");

                Snackbar.Add("Full sync completed but response was unexpected", Severity.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            lastSyncMessage = $"Connection error: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "HTTP error during full sync: {Message}", ex.Message);

            Snackbar.Add("Failed to connect to sync service", Severity.Error);
        }
        catch (Exception ex)
        {
            lastSyncMessage = $"Full sync failed: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "Error during full sync: {Message}", ex.Message);

            Snackbar.Add($"Full sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isAtumBatchSyncRunning = false;
            StateHasChanged();
        }
    }

    private void CancelAtumBatchSync()
    {
        if (batchSyncCancellationTokenSource != null && !batchSyncCancellationTokenSource.IsCancellationRequested)
        {
            Logger.LogInformation("User cancelled ATUM batch-by-batch sync");
            batchSyncCancellationTokenSource.Cancel();
        }
    }

    private async Task RefreshDashboard()
    {
        try
        {
            Logger.LogDebug("Refreshing dashboard data");

            // Load sync logs, product statistics, and auto-sync logs in parallel
            var syncLogsTask = SyncApi.GetSyncLogsAsync();
            var statisticsTask = SyncApi.GetProductStatisticsAsync();
            var autoSyncLogsTask = SyncApi.GetAutoSyncLogsAsync();

            await Task.WhenAll(syncLogsTask, statisticsTask, autoSyncLogsTask);

            var syncLogs = await syncLogsTask;
            productStatistics = await statisticsTask;
            var autoSyncLogs = await autoSyncLogsTask;

            if (syncLogs?.Any() == true)
            {
                lastSyncLog = syncLogs.OrderByDescending(s => s.StartedAt).First();
                Logger.LogDebug("Loaded last sync log: {SyncLogId} - {Status}", lastSyncLog.Id, lastSyncLog.Status);
            }
            else
            {
                lastSyncLog = null;
                Logger.LogDebug("No sync logs found");
            }

            if (autoSyncLogs?.Any() == true)
            {
                lastAutoSyncLog = autoSyncLogs.OrderByDescending(s => s.StartedAt).First();
                Logger.LogDebug("Loaded last auto-sync log: {AutoSyncLogId} - {Status}", lastAutoSyncLog.Id, lastAutoSyncLog.Status);
            }
            else
            {
                lastAutoSyncLog = null;
                Logger.LogDebug("No auto-sync logs found");
            }

            if (productStatistics != null)
            {
                Logger.LogDebug("Loaded product statistics: Total={Total}, SoftOne={SoftOne}, ATUM={ATUM}, WooCommerce={WooCommerce}",
                    productStatistics.Total, productStatistics.BySources.SoftOne, productStatistics.BySources.ATUM, productStatistics.BySources.WooCommerce);
            }
            else
            {
                Logger.LogDebug("No product statistics available");
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing dashboard: {Message}", ex.Message);
            Snackbar.Add("Failed to refresh dashboard data", Severity.Warning);
        }
    }

    private async Task LoadSyncHistory()
    {
        try
        {
            // For now, just refresh the dashboard
            // Later we can implement a dedicated sync history page/dialog
            await RefreshDashboard();
            Snackbar.Add("Dashboard refreshed with latest sync data", Severity.Info);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading sync history: {Message}", ex.Message);
            Snackbar.Add("Failed to load sync history", Severity.Error);
        }
    }

    private async Task TestConnections()
    {
        try
        {
            Snackbar.Add("Testing connections...", Severity.Info);

            // Test SoftOne connection
            var softOneResult = await SyncApi.TestConnectionAsync("softone");
            if (softOneResult.Success)
            {
                Snackbar.Add("SoftOne connection successful", Severity.Success);
            }
            else
            {
                Snackbar.Add($"SoftOne connection failed: {softOneResult.Message}", Severity.Error);
            }

            // Test WooCommerce connection
            var wooResult = await SyncApi.TestConnectionAsync("woocommerce");
            if (wooResult.Success)
            {
                Snackbar.Add("WooCommerce connection successful", Severity.Success);
            }
            else
            {
                Snackbar.Add($"WooCommerce connection failed: {wooResult.Message}", Severity.Error);
            }

            // Test email
            var emailResult = await SyncApi.TestEmailAsync();
            if (emailResult.Success)
            {
                Snackbar.Add("Email test successful", Severity.Success);
            }
            else
            {
                Snackbar.Add($"Email test failed: {emailResult.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error testing connections: {Message}", ex.Message);
            Snackbar.Add("Failed to test connections", Severity.Error);
        }
    }

    private static Color GetStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "completed" => Color.Success,
            "running" => Color.Info,
            "failed" => Color.Error,
            _ => Color.Default
        };
    }

    private static string GetDurationString(SyncLogResponse syncLog)
    {
        if (syncLog.CompletedAt.HasValue)
        {
            var duration = syncLog.CompletedAt.Value - syncLog.StartedAt;
            return duration.ToString(@"hh\:mm\:ss");
        }
        return "-";
    }

    private static Color GetAutoSyncStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "completed" => Color.Success,
            "running" => Color.Info,
            "failed" => Color.Error,
            "cancelled" => Color.Warning,
            _ => Color.Default
        };
    }

    private static string GetAutoSyncStatusText(string status)
    {
        return status.ToLower() switch
        {
            "completed" => "Ολοκληρώθηκε",
            "running" => "Εκτελείται",
            "failed" => "Αποτυχία",
            "cancelled" => "Ακυρώθηκε",
            _ => status
        };
    }

    private static string GetAutoSyncDuration(AutoSyncLog log)
    {
        if (log.CompletedAt.HasValue)
        {
            var duration = log.CompletedAt.Value - log.StartedAt;
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}ώ {duration.Minutes}λ";
            else if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}λ {duration.Seconds}δ";
            else
                return $"{duration.Seconds}δ";
        }
        return "Εκτελείται...";
    }

    private static Color GetSyncStepColor(string status)
    {
        return status.ToLower() switch
        {
            "success" => Color.Success,
            "failed" => Color.Error,
            "partial" => Color.Warning,
            _ => Color.Default
        };
    }

    private List<StoreDetailViewModel> GetStoreDetails(string detailsJson)
    {
        if (string.IsNullOrEmpty(detailsJson))
            return new List<StoreDetailViewModel>();

        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(detailsJson);
            if (dict == null) return new List<StoreDetailViewModel>();

            var results = new List<StoreDetailViewModel>();
            foreach (var kvp in dict)
            {
                var detail = kvp.Value;
                results.Add(new StoreDetailViewModel
                {
                    StoreName = detail.TryGetProperty("storeName", out var storeName) ? storeName.GetString() ?? "" : "",
                    StoreId = detail.TryGetProperty("storeId", out var storeId) ? storeId.GetInt32() : 0,
                    SoftOneSync = detail.TryGetProperty("softOneSync", out var softOne) ? softOne.GetString() ?? "" : "",
                    AtumSync = detail.TryGetProperty("atumSync", out var atum) ? atum.GetString() ?? "" : "",
                    FullSync = detail.TryGetProperty("fullSync", out var full) ? full.GetString() ?? "" : "",
                    Status = detail.TryGetProperty("status", out var status) ? status.GetString() ?? "" : ""
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing store details from JSON");
            return new List<StoreDetailViewModel>();
        }
    }

    public void Dispose()
    {
        wooCommerceProgressTimer?.Dispose();
        wooCommerceProgressTimer = null;
    }

    private class StoreDetailViewModel
    {
        public string StoreName { get; set; } = "";
        public int StoreId { get; set; }
        public string SoftOneSync { get; set; } = "";
        public string AtumSync { get; set; } = "";
        public string FullSync { get; set; } = "";
        public string Status { get; set; } = "";
    }
}