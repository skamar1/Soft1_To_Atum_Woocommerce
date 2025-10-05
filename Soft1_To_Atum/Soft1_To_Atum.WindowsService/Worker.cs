using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;

namespace Soft1_To_Atum.WindowsService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SyncServiceConfiguration _config;
    private Timer? _syncTimer;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IOptions<SyncServiceConfiguration> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Soft1ToAtumSyncService started at: {Time}", DateTimeOffset.Now);
        _logger.LogInformation("Sync interval: {Interval} minutes", _config.SyncSettings.IntervalMinutes);
        _logger.LogInformation("Auto-sync enabled: {Enabled}", _config.SyncSettings.EnableAutoSync);

        if (!_config.SyncSettings.EnableAutoSync)
        {
            _logger.LogInformation("Auto-sync is disabled. Service will not perform automatic synchronizations.");
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        // Initial sync on startup (wait 10 seconds first)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await PerformSync();

        // Setup periodic sync
        var interval = TimeSpan.FromMinutes(_config.SyncSettings.IntervalMinutes);
        _syncTimer = new Timer(
            async _ => await PerformSync(),
            null,
            interval,
            interval);

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task PerformSync()
    {
        using var scope = _serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Worker>>();

        try
        {
            logger.LogInformation("=== Starting Sync Cycle at {Time} ===", DateTimeOffset.Now);

            var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
            var softOneService = scope.ServiceProvider.GetRequiredService<SoftOneApiService>();
            var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceApiService>();
            var atumService = scope.ServiceProvider.GetRequiredService<AtumApiService>();

            // Step 1: Read from SoftOne
            logger.LogInformation("Step 1: Reading products from SoftOne...");
            var softOneProducts = await softOneService.GetProductsAsync(
                _config.SoftOne.BaseUrl,
                _config.SoftOne.AppId,
                _config.SoftOne.Token,
                _config.SoftOne.S1Code,
                "", // filters - empty for now
                default);

            logger.LogInformation("Retrieved {Count} products from SoftOne", softOneProducts.Count);

            // Step 2: Save to database
            logger.LogInformation("Step 2: Saving to database...");
            int savedCount = 0;
            foreach (var sp in softOneProducts)
            {
                var existing = await db.Products.FirstOrDefaultAsync(p => p.Sku == sp.Code);
                if (existing != null)
                {
                    existing.Quantity = sp.StockQuantity ?? 0;
                    existing.Price = sp.RetailPrice ?? 0;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.Products.Add(new Product
                    {
                        SoftOneId = sp.Code,
                        InternalId = sp.InternalId,
                        Sku = sp.Code,
                        Name = sp.Name,
                        Barcode = sp.Barcode,
                        Quantity = sp.StockQuantity ?? 0,
                        Price = sp.RetailPrice ?? 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                savedCount++;
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Saved {Count} products to database", savedCount);

            // TODO: Step 3-5 will be implemented in next phase
            // - WooCommerce matching/creation (parallel processing)
            // - ATUM inventory creation (batch processing)
            // - ATUM quantity updates (batch processing, skip if quantity unchanged)

            logger.LogInformation("=== Sync Cycle Completed Successfully at {Time} ===", DateTimeOffset.Now);
            logger.LogInformation("Summary: SoftOne={SoftOne} products synced to database", softOneProducts.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during sync cycle: {Message}", ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Soft1ToAtumSyncService is stopping at: {Time}", DateTimeOffset.Now);
        _syncTimer?.Dispose();
        await base.StopAsync(stoppingToken);
    }
}
