using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace Soft1_To_Atum.ApiService.Services;

public class AutoSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoSyncBackgroundService> _logger;
    private Timer? _timer;

    public AutoSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-Sync Background Service starting...");

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
                _logger.LogInformation("Auto-Sync Background Service is stopping due to cancellation.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Auto-Sync Background Service main loop");
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Auto-Sync Background Service has stopped.");
    }

    private async Task RunAutoSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
            var storeSettingsService = scope.ServiceProvider.GetRequiredService<StoreSettingsService>();
            var productMatchingService = scope.ServiceProvider.GetRequiredService<ProductMatchingService>();
            var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var atumApiService = scope.ServiceProvider.GetRequiredService<AtumApiService>();

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

            foreach (var store in enabledStores)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Auto-sync cancelled during store processing");
                    break;
                }

                _logger.LogInformation("=== Auto-Sync for Store: {StoreName} (ID: {StoreId}) ===", store.StoreName, store.Id);

                try
                {
                    // Step 1: SoftOne Sync
                    _logger.LogInformation("Step 1/3: Running SoftOne sync for store {StoreId}", store.Id);
                    await RunSoftOneSyncForStoreAsync(store.Id, scope.ServiceProvider, cancellationToken);

                    // Step 2: ATUM Sync
                    _logger.LogInformation("Step 2/3: Running ATUM sync for store {StoreId}", store.Id);
                    await RunAtumSyncForStoreAsync(store.Id, scope.ServiceProvider, cancellationToken);

                    // Step 3: Full Sync (WooCommerce + ATUM)
                    _logger.LogInformation("Step 3/3: Running Full sync for store {StoreId}", store.Id);
                    await RunFullSyncForStoreAsync(store.Id, scope.ServiceProvider, cancellationToken);

                    _logger.LogInformation("Auto-sync completed successfully for store {StoreName}", store.StoreName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-sync for store {StoreName} (ID: {StoreId})", store.StoreName, store.Id);
                    // Continue with next store even if this one fails
                }

                // Small delay between stores to avoid overwhelming the system
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            _logger.LogInformation("=== Auto-Sync completed for all enabled stores ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during auto-sync execution");
        }
    }

    private async Task RunSoftOneSyncForStoreAsync(int storeId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        try
        {
            // Call the internal API endpoint
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5465");

            var response = await client.PostAsync($"/api/sync/softone-to-database?storeId={storeId}", null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("SoftOne sync for store {StoreId} completed successfully", storeId);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("SoftOne sync for store {StoreId} failed with status {StatusCode}: {Error}",
                    storeId, response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SoftOne sync for store {StoreId}", storeId);
            throw;
        }
    }

    private async Task RunAtumSyncForStoreAsync(int storeId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        try
        {
            // Call the internal API endpoint
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5465");

            var response = await client.PostAsync($"/api/sync/atum?storeId={storeId}", null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("ATUM sync for store {StoreId} completed successfully", storeId);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("ATUM sync for store {StoreId} failed with status {StatusCode}: {Error}",
                    storeId, response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ATUM sync for store {StoreId}", storeId);
            throw;
        }
    }

    private async Task RunFullSyncForStoreAsync(int storeId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        try
        {
            // Call the internal API endpoint
            using var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5465");
            client.Timeout = TimeSpan.FromMinutes(30); // Full sync can take a long time

            var response = await client.GetAsync($"/api/sync/test-read-products?storeId={storeId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Full sync for store {StoreId} completed successfully", storeId);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Full sync for store {StoreId} failed with status {StatusCode}: {Error}",
                    storeId, response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Full sync for store {StoreId}", storeId);
            throw;
        }
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}
