using Microsoft.EntityFrameworkCore;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;

namespace Soft1_To_Atum.Worker;

public class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SyncWorker(ILogger<SyncWorker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan syncInterval = TimeSpan.FromMinutes(15); // Default fallback

        // Get sync interval from database
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
            var settings = await settingsService.GetAppSettingsAsync();

            if (settings.SyncAutoSync)
            {
                syncInterval = TimeSpan.FromMinutes(settings.SyncIntervalMinutes);
                _logger.LogInformation("SyncWorker started. Auto-sync enabled. Sync interval: {interval} minutes", syncInterval.TotalMinutes);
            }
            else
            {
                _logger.LogInformation("SyncWorker started. Auto-sync is DISABLED. Worker will check settings every 5 minutes.");
                syncInterval = TimeSpan.FromMinutes(5); // Check settings every 5 minutes when auto-sync is disabled
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading settings from database, using default interval of 15 minutes");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Re-read settings to check if they changed
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
                var currentSettings = await settingsService.GetAppSettingsAsync();

                if (currentSettings.SyncAutoSync)
                {
                    // Update sync interval if it changed
                    var newInterval = TimeSpan.FromMinutes(currentSettings.SyncIntervalMinutes);
                    if (newInterval != syncInterval)
                    {
                        syncInterval = newInterval;
                        _logger.LogInformation("Sync interval updated to {interval} minutes", syncInterval.TotalMinutes);
                    }

                    // Perform sync
                    await PerformSyncAsync(stoppingToken, currentSettings);
                }
                else
                {
                    _logger.LogDebug("Auto-sync is disabled, skipping sync operation");
                    syncInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes when disabled
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync operation");
            }

            await Task.Delay(syncInterval, stoppingToken);
        }
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken, AppSettings settings)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var syncLog = new SyncLog
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            TotalProducts = 0,
            CreatedProducts = 0,
            UpdatedProducts = 0,
            SkippedProducts = 0,
            ErrorCount = 0
        };

        dbContext.SyncLogs.Add(syncLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Starting sync operation with store: {StoreName}. SyncLog ID: {syncLogId}",
            settings.StoreName, syncLog.Id);

        try
        {
            // Validate required settings
            if (string.IsNullOrEmpty(settings.SoftOneGoBaseUrl) || string.IsNullOrEmpty(settings.SoftOneGoToken))
            {
                throw new InvalidOperationException("SoftOne Go settings are not configured properly");
            }

            if (string.IsNullOrEmpty(settings.WooCommerceUrl) || string.IsNullOrEmpty(settings.WooCommerceConsumerKey))
            {
                throw new InvalidOperationException("WooCommerce settings are not configured properly");
            }

            _logger.LogInformation("Using SoftOne Go API: {BaseUrl}", settings.SoftOneGoBaseUrl);
            _logger.LogInformation("Using WooCommerce API: {Url}", settings.WooCommerceUrl);
            _logger.LogInformation("Using ATUM Location: {LocationId} ({LocationName})",
                settings.AtumLocationId, settings.AtumLocationName);

            // TODO: Implement actual sync logic here
            // 1. Connect to SoftOne Go API using settings.SoftOneGo*
            // 2. Fetch products using settings.SoftOneGoFilters
            // 3. Connect to WooCommerce API using settings.WooCommerce*
            // 4. Update/create products using settings.Matching* and settings.FieldMapping*
            // 5. Update ATUM inventory using settings.Atum*
            // 6. Send email notifications if settings.SyncEmailNotifications is true

            // For now, simulate work
            await Task.Delay(2000, cancellationToken);

            // Update sync statistics
            syncLog.TotalProducts = 10;
            syncLog.CreatedProducts = 3;
            syncLog.UpdatedProducts = 5;
            syncLog.SkippedProducts = 2;
            syncLog.Status = "Completed";
            syncLog.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Sync completed successfully for store: {StoreName}. Duration: {duration}, Products: {total} ({created} created, {updated} updated, {skipped} skipped)",
                settings.StoreName, syncLog.Duration?.ToString(@"hh\:mm\:ss"),
                syncLog.TotalProducts, syncLog.CreatedProducts, syncLog.UpdatedProducts, syncLog.SkippedProducts);
        }
        catch (Exception ex)
        {
            syncLog.Status = "Failed";
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.ErrorDetails = ex.Message;
            syncLog.ErrorCount++;

            _logger.LogError(ex, "Sync operation failed");
            throw;
        }
        finally
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
