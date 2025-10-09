using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Soft1_To_Atum.WindowsService;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public Worker(
        IServiceProvider serviceProvider,
        ILogger<Worker> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Soft1ToAtumSyncService (Windows Service) starting at: {Time}", DateTimeOffset.Now);

        // Wait a bit on startup to let the application fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();

                var settings = await settingsService.GetAppSettingsAsync();

                if (settings.SyncAutoSync)
                {
                    _logger.LogInformation("Auto-Sync is enabled. Interval: {Interval} minutes", settings.SyncIntervalMinutes);

                    // Run sync immediately on startup if enabled
                    await RunAutoSyncAsync(stoppingToken);

                    // Then run on schedule
                    var intervalMinutes = settings.SyncIntervalMinutes > 0 ? settings.SyncIntervalMinutes : 15;
                    var interval = TimeSpan.FromMinutes(intervalMinutes);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(interval, stoppingToken);

                        // Re-check if auto-sync is still enabled
                        settings = await settingsService.GetAppSettingsAsync();
                        if (settings.SyncAutoSync)
                        {
                            await RunAutoSyncAsync(stoppingToken);
                        }
                        else
                        {
                            _logger.LogInformation("Auto-Sync has been disabled. Stopping scheduled syncs.");
                            break;
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Auto-Sync is disabled. Waiting for it to be enabled...");
                    // Check every minute if auto-sync has been enabled
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when the service is stopping
                _logger.LogInformation("Soft1ToAtumSyncService is stopping due to cancellation.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Auto-Sync main loop");
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Soft1ToAtumSyncService has stopped.");
    }

    private async Task RunAutoSyncAsync(CancellationToken cancellationToken)
    {
        AutoSyncLog? syncLog = null;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
            var storeSettingsService = scope.ServiceProvider.GetRequiredService<StoreSettingsService>();

            _logger.LogInformation("=== Starting Auto-Sync for all enabled stores ===");

            // Get all enabled stores
            var stores = await storeSettingsService.GetAllStoresAsync();
            var enabledStores = stores.Where(s => s.StoreEnabled).ToList();

            if (!enabledStores.Any())
            {
                _logger.LogWarning("No enabled stores found. Skipping auto-sync.");
                return;
            }

            _logger.LogInformation("Found {Count} enabled stores for auto-sync", enabledStores.Count);

            // Create sync log entry
            syncLog = new AutoSyncLog
            {
                StartedAt = DateTime.UtcNow,
                Status = "Running",
                TotalStores = enabledStores.Count,
                SuccessfulStores = 0,
                FailedStores = 0
            };
            dbContext.AutoSyncLogs.Add(syncLog);
            await dbContext.SaveChangesAsync(cancellationToken);

            var storeResults = new Dictionary<string, object>();
            int successCount = 0;
            int failCount = 0;

            foreach (var store in enabledStores)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Auto-sync cancelled during store processing");
                    syncLog.Status = "Cancelled";
                    syncLog.ErrorMessage = "Sync was cancelled during execution";
                    break;
                }

                _logger.LogInformation("=== Auto-Sync for Store: {StoreName} (ID: {StoreId}) ===", store.StoreName, store.Id);

                var storeResult = new Dictionary<string, object>
                {
                    ["storeName"] = store.StoreName,
                    ["storeId"] = store.Id
                };

                try
                {
                    // Step 1: SoftOne Sync
                    _logger.LogInformation("Step 1/3: Running SoftOne sync for store {StoreId}", store.Id);
                    var softOneSuccess = await RunSoftOneSyncForStoreAsync(store.Id, cancellationToken);
                    storeResult["softOneSync"] = softOneSuccess ? "Success" : "Failed";

                    // Step 2: ATUM Sync
                    _logger.LogInformation("Step 2/3: Running ATUM sync for store {StoreId}", store.Id);
                    var atumSuccess = await RunAtumSyncForStoreAsync(store.Id, cancellationToken);
                    storeResult["atumSync"] = atumSuccess ? "Success" : "Failed";

                    // Step 3: Full Sync (WooCommerce + ATUM)
                    _logger.LogInformation("Step 3/3: Running Full sync for store {StoreId}", store.Id);
                    var fullSyncSuccess = await RunFullSyncForStoreAsync(store.Id, cancellationToken);
                    storeResult["fullSync"] = fullSyncSuccess ? "Success" : "Failed";

                    if (softOneSuccess && atumSuccess && fullSyncSuccess)
                    {
                        storeResult["status"] = "Success";
                        successCount++;
                        _logger.LogInformation("Auto-sync completed successfully for store {StoreName}", store.StoreName);
                    }
                    else
                    {
                        storeResult["status"] = "Partial";
                        failCount++;
                        _logger.LogWarning("Auto-sync completed with some failures for store {StoreName}", store.StoreName);
                    }
                }
                catch (Exception ex)
                {
                    storeResult["status"] = "Failed";
                    storeResult["error"] = ex.Message;
                    failCount++;
                    _logger.LogError(ex, "Error during auto-sync for store {StoreName} (ID: {StoreId})", store.StoreName, store.Id);
                }

                storeResults[store.Id.ToString()] = storeResult;

                // Small delay between stores to avoid overwhelming the system
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            // Update sync log with results
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.Status = syncLog.Status == "Cancelled" ? "Cancelled" : (failCount == 0 ? "Completed" : "Failed");
            syncLog.SuccessfulStores = successCount;
            syncLog.FailedStores = failCount;
            syncLog.Details = JsonSerializer.Serialize(storeResults);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("=== Auto-Sync completed for all enabled stores ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during auto-sync execution");

            if (syncLog != null)
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

                syncLog.CompletedAt = DateTime.UtcNow;
                syncLog.Status = "Failed";
                syncLog.ErrorMessage = ex.Message;
                dbContext.Update(syncLog);
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private async Task<bool> RunSoftOneSyncForStoreAsync(int storeId, CancellationToken cancellationToken)
    {
        try
        {
            // Call the internal API endpoint
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://localhost:5465");

            var response = await client.PostAsync($"/api/sync/softone-to-database?storeId={storeId}", null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("SoftOne sync for store {StoreId} completed successfully", storeId);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("SoftOne sync for store {StoreId} failed with status {StatusCode}: {Error}",
                    storeId, response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SoftOne sync for store {StoreId}", storeId);
            return false;
        }
    }

    private async Task<bool> RunAtumSyncForStoreAsync(int storeId, CancellationToken cancellationToken)
    {
        try
        {
            // Call the internal API endpoint
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://localhost:5465");

            var response = await client.PostAsync($"/api/sync/atum?storeId={storeId}", null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("ATUM sync for store {StoreId} completed successfully", storeId);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("ATUM sync for store {StoreId} failed with status {StatusCode}: {Error}",
                    storeId, response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ATUM sync for store {StoreId}", storeId);
            return false;
        }
    }

    private async Task<bool> RunFullSyncForStoreAsync(int storeId, CancellationToken cancellationToken)
    {
        try
        {
            // Call the internal API endpoint
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://localhost:5465");
            client.Timeout = TimeSpan.FromMinutes(30); // Full sync can take a long time

            var response = await client.GetAsync($"/api/sync/test-read-products?storeId={storeId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Full sync for store {StoreId} completed successfully", storeId);
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Full sync for store {StoreId} failed with status {StatusCode}: {Error}",
                    storeId, response.StatusCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Full sync for store {StoreId}", storeId);
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Soft1ToAtumSyncService is stopping at: {Time}", DateTimeOffset.Now);
        await base.StopAsync(stoppingToken);
    }
}
