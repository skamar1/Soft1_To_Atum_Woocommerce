using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Soft1_To_Atum.ApiService.Extensions;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add only the services we need from ServiceDefaults, WITHOUT the automatic resilience handlers
// that add 10-second timeouts to all HttpClients
builder.ConfigureOpenTelemetry();
builder.AddDefaultHealthChecks();
builder.Services.AddServiceDiscovery();

builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlite("Data Source=sync.db"));

// Add services
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SoftOneApiService>();
builder.Services.AddScoped<AtumApiService>();
builder.Services.AddScoped<WooCommerceApiService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ProductMatchingService>();
builder.Services.AddScoped<IWooCommerceAtumClient, WooCommerceAtumClient>();
builder.Services.AddHttpClient();

// Configure specific HttpClient for WooCommerce with very extended timeout
builder.Services.AddHttpClient<WooCommerceApiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(20); // 20 minutes total timeout for very slow WooCommerce API
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    ConnectTimeout = TimeSpan.FromSeconds(30)
})
.AddStandardResilienceHandler(options =>
{
    // Minimal retry - we handle pagination manually
    options.Retry.MaxRetryAttempts = 1;
    options.Retry.Delay = TimeSpan.FromSeconds(2);

    // Very long timeout for each attempt (one page can take up to 10 minutes!)
    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(10);

    // Total timeout must cover the entire request
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(15);

    // Configure circuit breaker (sampling must be at least 2x attempt timeout)
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(20);
    options.CircuitBreaker.FailureRatio = 0.99; // Almost never break - server is just slow
    options.CircuitBreaker.MinimumThroughput = 10;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() {
        Title = "SoftOne to ATUM Sync API",
        Version = "v1",
        Description = "API for synchronizing products between SoftOne Go and WooCommerce ATUM"
    });
});

var app = builder.Build();

// Initialize database in background to avoid blocking startup
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting database initialization...");

        // Ensure database exists first
        await dbContext.Database.EnsureCreatedAsync();
        
        // Use the SettingsService to handle AppSettings initialization
        var settingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
        await settingsService.GetAppSettingsAsync(); // This will create default settings if they don't exist

        // Ensure there's at least one active Store
        var existingStore = await dbContext.Stores.FirstOrDefaultAsync(s => s.IsActive);
        if (existingStore == null)
        {
            logger.LogInformation("No active store found, creating default store...");

            var defaultStore = new Store
            {
                Name = "Κατάστημα Κέντρο",
                WooCommerceUrl = "https://your-woocommerce-site.com",
                WooCommerceKey = "ck_your_consumer_key",
                WooCommerceSecret = "cs_your_consumer_secret",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Stores.Add(defaultStore);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Default store created with ID: {StoreId}", defaultStore.Id);
        }

        logger.LogInformation("Database initialization completed successfully");
    }
    catch (Exception ex)
    {
        // Log the error but don't crash the application
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error during database initialization: {Message}", ex.Message);
    }
});

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SoftOne to ATUM Sync API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapGet("/health", () => "Healthy")
.WithTags("Health")
.WithSummary("Health check endpoint");

app.MapGet("/debug/test", () => new { message = "API is working", timestamp = DateTime.UtcNow })
.WithTags("Debug")
.WithSummary("Simple test endpoint");

// API endpoint groups
var syncGroup = app.MapGroup("/api/sync")
    .WithTags("Sync")
    .WithOpenApi();

var productsGroup = app.MapGroup("/api/products")
    .WithTags("Products")
    .WithOpenApi();

var storesGroup = app.MapGroup("/api/stores")
    .WithTags("Stores")
    .WithOpenApi();

var settingsGroup = app.MapGroup("/api/settings")
    .WithTags("Settings")
    .WithOpenApi();

syncGroup.MapGet("/logs", async (SyncDbContext db) =>
{
    var logs = await db.SyncLogs
        .OrderByDescending(l => l.StartedAt)
        .Take(50)
        .ToListAsync();
    return Results.Ok(logs);
})
.WithName("GetSyncLogs")
.WithSummary("Get the latest 50 sync logs")
.WithDescription("Retrieves the 50 most recent synchronization logs");

syncGroup.MapGet("/logs/{id:int}", async (int id, SyncDbContext db) =>
{
    var log = await db.SyncLogs.FindAsync(id);
    return log != null ? Results.Ok(log) : Results.NotFound();
})
.WithName("GetSyncLog")
.WithSummary("Get a specific sync log by ID")
.WithDescription("Retrieves a synchronization log by its unique identifier");

syncGroup.MapGet("/status", async (SyncDbContext db) =>
{
    var lastSync = await db.SyncLogs
        .OrderByDescending(l => l.StartedAt)
        .FirstOrDefaultAsync();

    var isRunning = lastSync?.Status == "Running";
    var totalProducts = await db.Products.CountAsync();

    var stats = new
    {
        IsRunning = isRunning,
        LastSyncAt = lastSync?.StartedAt,
        LastSyncStatus = lastSync?.Status,
        LastSyncDuration = lastSync?.Duration?.ToString(@"hh\:mm\:ss"),
        TotalProducts = totalProducts,
        LastSyncStats = lastSync != null ? new
        {
            Total = lastSync.TotalProducts,
            Created = lastSync.CreatedProducts,
            Updated = lastSync.UpdatedProducts,
            Skipped = lastSync.SkippedProducts,
            Errors = lastSync.ErrorCount
        } : null
    };

    return Results.Ok(stats);
})
.WithName("GetSyncStatus")
.WithSummary("Get current sync status and statistics")
.WithDescription("Retrieves the current synchronization status, last sync information, and product statistics");

// Βήμα 1+2+3: Διάβασμα από βάση + WooCommerce matching/creation + ATUM creation
syncGroup.MapGet("/test-read-products", async (
    SyncDbContext db,
    WooCommerceApiService wooCommerceService,
    AtumApiService atumApiService,
    SettingsService settingsService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("=== Full Sync: DB → WooCommerce → ATUM (Create + Update) ===");

    try
    {
        // 1. Διαβάζουμε όλα τα προϊόντα από τη βάση
        var allProducts = await db.Products.ToListAsync(cancellationToken);
        logger.LogInformation("Read {Count} products from database", allProducts.Count);

        // 2. Βρίσκουμε προϊόντα χωρίς WooCommerce ID
        var productsWithoutWooId = allProducts.Where(p => string.IsNullOrEmpty(p.WooCommerceId)).ToList();
        logger.LogInformation("Found {Count} products without WooCommerce ID", productsWithoutWooId.Count);

        // 3. Get settings για WooCommerce credentials
        var settings = await settingsService.GetAppSettingsAsync();
        if (settings == null || string.IsNullOrEmpty(settings.WooCommerceConsumerKey))
        {
            logger.LogWarning("WooCommerce credentials not configured, skipping WooCommerce matching");
            return Results.Ok(new
            {
                Success = true,
                TotalProducts = allProducts.Count,
                ProductsChecked = 0,
                MatchedInWooCommerce = 0,
                CreatedInWooCommerce = 0,
                Errors = 0,
                Message = "WooCommerce credentials not configured",
                SampleProducts = allProducts.Take(10).Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Sku,
                    p.InternalId,
                    p.WooCommerceId,
                    p.AtumId,
                    SoftOneQuantity = p.Quantity,
                    AtumQuantity = p.AtumQuantity,
                    p.Price
                }).ToList()
            });
        }

        // 4. Παράλληλα requests στο WooCommerce (max 10 ταυτόχρονα)
        int matchedCount = 0;
        int createdCount = 0;
        int errorCount = 0;
        var semaphore = new SemaphoreSlim(10, 10);
        var tasks = new List<Task>();

        foreach (var product in productsWithoutWooId)
        {
            if (string.IsNullOrEmpty(product.Sku))
            {
                logger.LogWarning("Product {Name} has no SKU, skipping", product.Name);
                errorCount++;
                continue;
            }

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // Ψάχνουμε στο WooCommerce με SKU
                    var wooProduct = await wooCommerceService.GetProductBySkuAsync(
                        settings.WooCommerceConsumerKey,
                        settings.WooCommerceConsumerSecret,
                        product.Sku,
                        cancellationToken);

                    if (wooProduct != null && wooProduct.Id > 0)
                    {
                        // Βρέθηκε - ενημερώνουμε τη βάση
                        product.WooCommerceId = wooProduct.Id.ToString();
                        product.UpdatedAt = DateTime.UtcNow;
                        product.LastSyncStatus = "Matched in WooCommerce";
                        Interlocked.Increment(ref matchedCount);
                        logger.LogInformation("Matched product {Name} (SKU: {Sku}) -> WooCommerce ID: {WooId}",
                            product.Name, product.Sku, wooProduct.Id);
                    }
                    else
                    {
                        // Δεν βρέθηκε - δημιουργούμε draft
                        logger.LogInformation("Creating draft product in WooCommerce: {Name} (SKU: {Sku})",
                            product.Name, product.Sku);

                        var newProduct = await wooCommerceService.CreateProductAsync(
                            settings.WooCommerceConsumerKey,
                            settings.WooCommerceConsumerSecret,
                            product.Name,
                            product.Sku,
                            product.Price,
                            cancellationToken);

                        if (newProduct != null && newProduct.Id > 0)
                        {
                            product.WooCommerceId = newProduct.Id.ToString();
                            product.UpdatedAt = DateTime.UtcNow;
                            product.LastSyncStatus = "Created as draft in WooCommerce";
                            Interlocked.Increment(ref createdCount);
                            logger.LogInformation("Created draft product {Name} -> WooCommerce ID: {WooId}",
                                product.Name, newProduct.Id);
                        }
                        else
                        {
                            product.LastSyncStatus = "Error - Failed to create in WooCommerce";
                            Interlocked.Increment(ref errorCount);
                            logger.LogWarning("Failed to create product {Name} in WooCommerce", product.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing product {Name} (SKU: {Sku})", product.Name, product.Sku);
                    product.LastSyncStatus = $"Error - {ex.Message}";
                    Interlocked.Increment(ref errorCount);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        // Περιμένουμε όλα τα tasks να ολοκληρωθούν
        await Task.WhenAll(tasks);

        // 5. Αποθηκεύουμε τις αλλαγές στη βάση (WooCommerce matching)
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved WooCommerce changes to database");

        // 6. ATUM Creation: Δημιουργία προϊόντων στο ATUM
        logger.LogInformation("=== Starting ATUM product creation ===");
        int createdInAtum = 0;
        int atumErrors = 0;

        // Βρίσκουμε προϊόντα με WooCommerceId αλλά χωρίς AtumId
        var productsForAtum = allProducts
            .Where(p => !string.IsNullOrEmpty(p.WooCommerceId) && string.IsNullOrEmpty(p.AtumId))
            .ToList();

        logger.LogInformation("Found {Count} products to create in ATUM", productsForAtum.Count);

        if (productsForAtum.Any())
        {
            try
            {
                // Σπάμε σε batches των 50 items για να μην υπερφορτώσουμε το ATUM API
                const int batchSize = 50;
                int totalBatches = (int)Math.Ceiling((double)productsForAtum.Count / batchSize);

                logger.LogInformation("Processing {TotalProducts} products in {TotalBatches} batches of {BatchSize} items each",
                    productsForAtum.Count, totalBatches, batchSize);

                for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                {
                    var currentBatchProducts = productsForAtum
                        .Skip(batchIndex * batchSize)
                        .Take(batchSize)
                        .ToList();

                    logger.LogInformation("=== Processing ATUM Batch {CurrentBatch}/{TotalBatches} with {Count} products ===",
                        batchIndex + 1, totalBatches, currentBatchProducts.Count);

                    // Δημιουργούμε το batch request για ATUM
                    var batchRequest = new AtumBatchRequest();

                    foreach (var product in currentBatchProducts)
                    {
                        if (!int.TryParse(product.WooCommerceId, out int wooCommerceProductId))
                        {
                            logger.LogWarning("Invalid WooCommerce ID for product {Name}: {WooId}", product.Name, product.WooCommerceId);
                            atumErrors++;
                            continue;
                        }

                        var createItem = new AtumBatchCreateItem
                        {
                            ProductId = wooCommerceProductId,
                            Name = settings.AtumLocationName ?? "store1_location", // Location name, not product name
                            IsMain = false,
                            Location = new List<int> { settings.AtumLocationId },
                            MetaData = new AtumBatchMetaData
                            {
                                Sku = product.Sku ?? "",
                                ManageStock = true,
                                StockQuantity = product.Quantity, // Ποσότητα από SoftOne
                                Backorders = false,
                                StockStatus = product.Quantity > 0 ? "instock" : "outofstock",
                                Barcode = product.Barcode ?? ""
                            }
                        };

                        batchRequest.Create.Add(createItem);
                    }

                    if (batchRequest.Create.Any())
                    {
                        logger.LogInformation("Sending ATUM batch {BatchNum} with {Count} items to API",
                            batchIndex + 1, batchRequest.Create.Count);

                        try
                        {
                            // Καλούμε το ATUM API
                            var batchResponse = await atumApiService.BatchUpdateInventoryAsync(
                                settings.WooCommerceConsumerKey,
                                settings.WooCommerceConsumerSecret,
                                batchRequest,
                                cancellationToken);

                            logger.LogInformation("ATUM API returned response for batch {BatchNum}. Create count: {CreateCount}",
                                batchIndex + 1, batchResponse.Create?.Count ?? 0);

                            // Ενημερώνουμε τη βάση με τα ATUM IDs
                            if (batchResponse.Create != null && batchResponse.Create.Any())
                            {
                                foreach (var createdItem in batchResponse.Create)
                                {
                                    if (createdItem.Error != null)
                                    {
                                        logger.LogError("ATUM creation error for product {ProductId}: {ErrorCode} - {ErrorMessage}",
                                            createdItem.ProductId, createdItem.Error.Code, createdItem.Error.Message);
                                        atumErrors++;
                                        continue;
                                    }

                                    // Βρίσκουμε το προϊόν στη βάση με WooCommerceId
                                    var product = allProducts.FirstOrDefault(p => p.WooCommerceId == createdItem.ProductId.ToString());
                                    if (product != null)
                                    {
                                        product.AtumId = createdItem.Id.ToString();
                                        product.AtumQuantity = product.Quantity; // Ενημέρωση με την ποσότητα που στείλαμε
                                        product.UpdatedAt = DateTime.UtcNow;
                                        product.LastSyncedAt = DateTime.UtcNow;
                                        product.LastSyncStatus = "Created in ATUM";
                                        createdInAtum++;

                                        logger.LogDebug("Created in ATUM: {Name} (WooCommerce ID: {WooId}) -> ATUM ID: {AtumId}",
                                            product.Name, product.WooCommerceId, createdItem.Id);
                                    }
                                    else
                                    {
                                        logger.LogWarning("Product with WooCommerce ID {WooId} not found in database after ATUM creation",
                                            createdItem.ProductId);
                                    }
                                }

                                // Αποθηκεύουμε τις αλλαγές στη βάση μετά από κάθε batch
                                await db.SaveChangesAsync(cancellationToken);
                                logger.LogInformation("Batch {BatchNum} completed: {Created} created in this batch. Total so far: {TotalCreated} created, {TotalErrors} errors",
                                    batchIndex + 1, batchResponse.Create.Count, createdInAtum, atumErrors);
                            }
                            else
                            {
                                logger.LogWarning("ATUM API returned empty or null Create list for batch {BatchNum}", batchIndex + 1);
                            }
                        }
                        catch (Exception batchEx)
                        {
                            logger.LogError(batchEx, "Error processing ATUM batch {BatchNum}: {Message}",
                                batchIndex + 1, batchEx.Message);
                            atumErrors += currentBatchProducts.Count;
                        }

                        // Μικρή καθυστέρηση μεταξύ batches για να μην υπερφορτώσουμε το API
                        if (batchIndex < totalBatches - 1)
                        {
                            await Task.Delay(500, cancellationToken);
                        }
                    }
                }

                logger.LogInformation("=== ATUM batch processing completed: {TotalCreated} total created, {TotalErrors} total errors ===",
                    createdInAtum, atumErrors);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during ATUM batch creation");
                atumErrors += productsForAtum.Count - createdInAtum;
            }
        }

        // 7. ATUM Update: Ενημέρωση ποσοτήτων για προϊόντα με όλα τα IDs
        logger.LogInformation("=== Starting ATUM quantity update ===");
        int updatedInAtum = 0;
        int atumUpdateErrors = 0;

        // Βρίσκουμε προϊόντα που έχουν όλα τα IDs (SoftOne/Internal, WooCommerce, ATUM)
        var productsForAtumUpdate = allProducts
            .Where(p => (!string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId)) &&
                       !string.IsNullOrEmpty(p.WooCommerceId) &&
                       !string.IsNullOrEmpty(p.AtumId))
            .ToList();

        logger.LogInformation("Found {Count} products to update in ATUM", productsForAtumUpdate.Count);

        if (productsForAtumUpdate.Any())
        {
            try
            {
                // Σπάμε σε batches των 50 items
                const int batchSize = 50;
                int totalBatches = (int)Math.Ceiling((double)productsForAtumUpdate.Count / batchSize);

                logger.LogInformation("Processing {TotalProducts} products in {TotalBatches} update batches of {BatchSize} items each",
                    productsForAtumUpdate.Count, totalBatches, batchSize);

                for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                {
                    var currentBatchProducts = productsForAtumUpdate
                        .Skip(batchIndex * batchSize)
                        .Take(batchSize)
                        .ToList();

                    logger.LogInformation("=== Processing ATUM Update Batch {CurrentBatch}/{TotalBatches} with {Count} products ===",
                        batchIndex + 1, totalBatches, currentBatchProducts.Count);

                    // Δημιουργούμε το batch update request για ATUM
                    var batchRequest = new AtumBatchRequest();

                    foreach (var product in currentBatchProducts)
                    {
                        if (!int.TryParse(product.AtumId, out int atumInventoryId))
                        {
                            logger.LogWarning("Invalid ATUM ID for product {Name}: {AtumId}", product.Name, product.AtumId);
                            atumUpdateErrors++;
                            continue;
                        }

                        var updateItem = new AtumBatchUpdateItem
                        {
                            Id = atumInventoryId,
                            MetaData = new AtumBatchUpdateMetaData
                            {
                                StockQuantity = product.Quantity // Ποσότητα από SoftOne
                            }
                        };

                        batchRequest.Update.Add(updateItem);
                    }

                    if (batchRequest.Update.Any())
                    {
                        logger.LogInformation("Sending ATUM update batch {BatchNum} with {Count} items to API",
                            batchIndex + 1, batchRequest.Update.Count);

                        try
                        {
                            // Καλούμε το ATUM API
                            var batchResponse = await atumApiService.BatchUpdateInventoryAsync(
                                settings.WooCommerceConsumerKey,
                                settings.WooCommerceConsumerSecret,
                                batchRequest,
                                cancellationToken);

                            logger.LogInformation("ATUM API returned update response for batch {BatchNum}. Update count: {UpdateCount}",
                                batchIndex + 1, batchResponse.Update?.Count ?? 0);

                            // Ενημερώνουμε τη βάση
                            if (batchResponse.Update != null && batchResponse.Update.Any())
                            {
                                foreach (var updatedItem in batchResponse.Update)
                                {
                                    if (updatedItem.Error != null)
                                    {
                                        logger.LogError("ATUM update error for inventory {InventoryId}: {ErrorCode} - {ErrorMessage}",
                                            updatedItem.Id, updatedItem.Error.Code, updatedItem.Error.Message);
                                        atumUpdateErrors++;
                                        continue;
                                    }

                                    // Βρίσκουμε το προϊόν στη βάση με AtumId
                                    var product = allProducts.FirstOrDefault(p => p.AtumId == updatedItem.Id.ToString());
                                    if (product != null)
                                    {
                                        product.AtumQuantity = product.Quantity; // Sync με την ποσότητα που στείλαμε
                                        product.UpdatedAt = DateTime.UtcNow;
                                        product.LastSyncedAt = DateTime.UtcNow;
                                        product.LastSyncStatus = "ATUM: Quantity updated";
                                        updatedInAtum++;

                                        logger.LogDebug("Updated ATUM quantity: {Name} (ATUM ID: {AtumId}) -> Quantity: {Quantity}",
                                            product.Name, product.AtumId, product.Quantity);
                                    }
                                    else
                                    {
                                        logger.LogWarning("Product with ATUM ID {AtumId} not found in database after update",
                                            updatedItem.Id);
                                    }
                                }

                                // Αποθηκεύουμε τις αλλαγές στη βάση μετά από κάθε batch
                                await db.SaveChangesAsync(cancellationToken);
                                logger.LogInformation("Update batch {BatchNum} completed: {Updated} updated in this batch. Total so far: {TotalUpdated} updated, {TotalErrors} errors",
                                    batchIndex + 1, batchResponse.Update.Count, updatedInAtum, atumUpdateErrors);
                            }
                            else
                            {
                                logger.LogWarning("ATUM API returned empty or null Update list for batch {BatchNum}", batchIndex + 1);
                            }
                        }
                        catch (Exception batchEx)
                        {
                            logger.LogError(batchEx, "Error processing ATUM update batch {BatchNum}: {Message}",
                                batchIndex + 1, batchEx.Message);
                            atumUpdateErrors += currentBatchProducts.Count;
                        }

                        // Μικρή καθυστέρηση μεταξύ batches
                        if (batchIndex < totalBatches - 1)
                        {
                            await Task.Delay(500, cancellationToken);
                        }
                    }
                }

                logger.LogInformation("=== ATUM update processing completed: {TotalUpdated} total updated, {TotalErrors} total errors ===",
                    updatedInAtum, atumUpdateErrors);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during ATUM batch update");
                atumUpdateErrors += productsForAtumUpdate.Count - updatedInAtum;
            }
        }

        // 8. Επιστροφή αποτελεσμάτων
        var sampleProducts = allProducts.Take(10).Select(p => new
        {
            p.Id,
            p.Name,
            p.Sku,
            p.InternalId,
            p.WooCommerceId,
            p.AtumId,
            SoftOneQuantity = p.Quantity,
            AtumQuantity = p.AtumQuantity,
            p.Price,
            p.LastSyncStatus
        }).ToList();

        return Results.Ok(new
        {
            Success = true,
            TotalProducts = allProducts.Count,
            ProductsChecked = productsWithoutWooId.Count,
            MatchedInWooCommerce = matchedCount,
            CreatedInWooCommerce = createdCount,
            CreatedInAtum = createdInAtum,
            UpdatedInAtum = updatedInAtum,
            Errors = errorCount,
            AtumErrors = atumErrors,
            AtumUpdateErrors = atumUpdateErrors,
            Message = $"WooCommerce: {matchedCount} matched, {createdCount} created, {errorCount} errors | ATUM: {createdInAtum} created, {updatedInAtum} updated, {atumErrors + atumUpdateErrors} total errors",
            SampleProducts = sampleProducts
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during product sync");
        return Results.Problem($"Error: {ex.Message}");
    }
})
.WithName("TestReadProducts")
.WithSummary("Ολοκληρωμένος συγχρονισμός: DB → WooCommerce → ATUM (Create + Update)")
.WithDescription("Βήμα 1: Διαβάζει προϊόντα από βάση | Βήμα 2: Ψάχνει στο WooCommerce με SKU και δημιουργεί draft για νέα | Βήμα 3: Δημιουργεί inventory στο ATUM για προϊόντα με WooCommerce ID | Βήμα 4: Ενημερώνει ποσότητες στο ATUM για προϊόντα με όλα τα IDs");

// ATUM Sync: Διάβασμα από ATUM API και ενημέρωση βάσης
syncGroup.MapPost("/atum", async (
    SyncDbContext db,
    AtumApiService atumApiService,
    SettingsService settingsService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("=== ATUM SYNC: Reading from ATUM API and updating database ===");

    try
    {
        // 1. Get settings
        var settings = await settingsService.GetAppSettingsAsync();
        if (settings == null)
        {
            logger.LogError("Settings not found");
            return Results.Problem("Settings not configured");
        }

        if (string.IsNullOrEmpty(settings.WooCommerceConsumerKey) || string.IsNullOrEmpty(settings.WooCommerceConsumerSecret))
        {
            logger.LogError("WooCommerce credentials not configured");
            return Results.Problem("WooCommerce credentials not configured");
        }

        // 2. Fetch all ATUM inventory
        logger.LogInformation("Fetching all ATUM inventory for location {LocationId}", settings.AtumLocationId);
        var atumItems = await atumApiService.GetAllInventoryAsync(
            settings.WooCommerceConsumerKey,
            settings.WooCommerceConsumerSecret,
            settings.AtumLocationId,
            cancellationToken);

        logger.LogInformation("Retrieved {Count} items from ATUM API", atumItems.Count);

        if (atumItems.Count == 0)
        {
            return Results.Ok(new
            {
                Success = true,
                TotalAtumItems = 0,
                MatchedInDatabase = 0,
                NotFoundInDatabase = 0,
                Message = "No items found in ATUM inventory"
            });
        }

        // 3. Update database with ATUM data
        int matchedCount = 0;
        int notFoundCount = 0;

        foreach (var atumItem in atumItems)
        {
            try
            {
                var sku = atumItem.GetSku();
                if (string.IsNullOrEmpty(sku))
                {
                    logger.LogWarning("ATUM item {AtumId} has no SKU, skipping", atumItem.Id);
                    notFoundCount++;
                    continue;
                }

                // Find product in database by SKU
                var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);

                if (product != null)
                {
                    // Update with ATUM data
                    product.AtumId = atumItem.Id.ToString();
                    product.AtumQuantity = atumItem.GetStockQuantity();
                    product.UpdatedAt = DateTime.UtcNow;
                    product.LastSyncedAt = DateTime.UtcNow;
                    product.LastSyncStatus = "ATUM: Updated";

                    matchedCount++;
                    logger.LogDebug("Updated product {Name} (SKU: {Sku}) with ATUM ID: {AtumId}, Quantity: {Quantity}",
                        product.Name, sku, atumItem.Id, atumItem.GetStockQuantity());
                }
                else
                {
                    notFoundCount++;
                    logger.LogWarning("Product with SKU {Sku} not found in database (ATUM ID: {AtumId})",
                        sku, atumItem.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing ATUM item {AtumId}", atumItem.Id);
                notFoundCount++;
            }
        }

        // 4. Save changes to database
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved changes to database: {Matched} matched, {NotFound} not found",
            matchedCount, notFoundCount);

        return Results.Ok(new
        {
            Success = true,
            TotalAtumItems = atumItems.Count,
            MatchedInDatabase = matchedCount,
            NotFoundInDatabase = notFoundCount,
            Message = $"ATUM sync completed: {matchedCount} products updated, {notFoundCount} not found in database"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during ATUM sync: {Message}", ex.Message);
        return Results.Problem($"Error during ATUM sync: {ex.Message}");
    }
})
.WithName("SyncAtum")
.WithSummary("ATUM Sync: Διάβασμα από ATUM και ενημέρωση βάσης")
.WithDescription("Καλεί το ATUM API, παίρνει το inventory, και ενημερώνει τη βάση με ATUM IDs και quantities");

// Βήμα 2: Fetch από SoftOne API και αποθήκευση στη βάση
syncGroup.MapPost("/softone-to-database", async (
    SyncDbContext db,
    SettingsService settingsService,
    ProductMatchingService productMatchingService,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("=== ΒΗΜΑ 2: Fetch SoftOne → Database ===");

    try
    {
        // 1. Get settings
        var settings = await settingsService.GetAppSettingsAsync();
        if (settings == null)
        {
            logger.LogError("Settings not found");
            return Results.Problem("Settings not configured");
        }

        var apiSettings = settings.ToApiModel();
        if (string.IsNullOrEmpty(apiSettings.SoftOneGo.Token))
        {
            logger.LogError("SoftOne Go token is not configured");
            return Results.Problem("SoftOne Go token not configured");
        }

        // 2. Call SoftOne API
        logger.LogInformation("Calling SoftOne API...");
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiSettings.SoftOneGo.BaseUrl}/list/item");
        request.Headers.Add("s1code", apiSettings.SoftOneGo.S1Code);
        var content = new StringContent(
            $"{{\n    \"appId\": \"703\",\n    \"filters\": \"ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999\",\n    \"token\": \"{apiSettings.SoftOneGo.Token}\"\n}}",
            null,
            "application/json");
        request.Content = content;

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("SoftOne API error {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            return Results.Problem($"SoftOne API error: {response.StatusCode} - {errorContent}");
        }

        // 3. Parse response
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string responseContent;
        var contentEncoding = response.Content.Headers.ContentEncoding;

        if (contentEncoding.Contains("gzip"))
        {
            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var gzipStream = new System.IO.Compression.GZipStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream, Encoding.GetEncoding("windows-1253"));
            responseContent = await reader.ReadToEndAsync();
        }
        else
        {
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var encoding = Encoding.GetEncoding("windows-1253");
            responseContent = encoding.GetString(responseBytes);
        }

        logger.LogInformation("Received response from SoftOne API");

        // 4. Parse JSON
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        if (!root.TryGetProperty("success", out var successElement) || !successElement.GetBoolean())
        {
            logger.LogError("SoftOne API returned success=false");
            return Results.Problem("SoftOne API returned unsuccessful response");
        }

        var totalCount = root.TryGetProperty("totalcount", out var totalElement) ? totalElement.GetInt32() : 0;
        logger.LogInformation("SoftOne API returned {TotalCount} products", totalCount);

        if (!root.TryGetProperty("fields", out var fieldsElement) || !root.TryGetProperty("rows", out var rowsElement))
        {
            logger.LogError("Missing fields or rows in SoftOne API response");
            return Results.Problem("Missing fields or rows in SoftOne API response");
        }

        // 5. Extract field names
        var fieldNames = new List<string>();
        foreach (var field in fieldsElement.EnumerateArray())
        {
            if (field.TryGetProperty("name", out var nameElement))
            {
                fieldNames.Add(nameElement.GetString() ?? "");
            }
        }

        // 6. Convert rows to structured products
        var products = new List<Dictionary<string, string?>>();
        foreach (var row in rowsElement.EnumerateArray())
        {
            var product = new Dictionary<string, string?>();
            var values = row.EnumerateArray().ToList();

            for (int i = 0; i < fieldNames.Count && i < values.Count; i++)
            {
                var fieldName = fieldNames[i];
                var value = values[i].ValueKind == JsonValueKind.Null ? null : values[i].GetString();
                product[fieldName] = value;
            }

            products.Add(product);
        }

        logger.LogInformation("Parsed {ProductCount} products from SoftOne API", products.Count);

        // 7. Process and save to database
        int createdCount = 0;
        int updatedCount = 0;
        int errorCount = 0;

        foreach (var product in products)
        {
            try
            {
                var result = await productMatchingService.ProcessSoftOneProductAsync(product, cancellationToken);

                if (result.Success)
                {
                    if (result.Action == ProductAction.Created)
                        createdCount++;
                    else if (result.Action == ProductAction.Updated)
                        updatedCount++;

                    logger.LogDebug("Processed product {ProductId}: {Action}", result.Product.Id, result.Action);
                }
                else
                {
                    errorCount++;
                    logger.LogWarning("Failed to process product: {ErrorMessage}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                var productName = product.GetValueOrDefault("ITEM.NAME") ?? "Unknown";
                logger.LogError(ex, "Error processing product {ProductName}: {Message}", productName, ex.Message);
            }
        }

        logger.LogInformation("Completed: Created={Created}, Updated={Updated}, Errors={Errors}",
            createdCount, updatedCount, errorCount);

        return Results.Ok(new
        {
            Success = true,
            TotalFetched = products.Count,
            Created = createdCount,
            Updated = updatedCount,
            Errors = errorCount,
            Message = $"SoftOne sync completed. Created: {createdCount}, Updated: {updatedCount}, Errors: {errorCount}"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during SoftOne sync: {Message}", ex.Message);
        return Results.Problem($"Error during SoftOne sync: {ex.Message}");
    }
})
.WithName("SyncSoftOneToDatabase")
.WithSummary("ΒΗΜΑ 2: Fetch από SoftOne API και αποθήκευση στη βάση")
.WithDescription("Καλεί το SoftOne API, παίρνει προϊόντα, και τα αποθηκεύει/ενημερώνει στη βάση μας");

productsGroup.MapGet("/", async (SyncDbContext db, int page = 1, int pageSize = 50) =>
{
    try
    {
        var products = await db.Products
            .OrderByDescending(p => p.LastSyncedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await db.Products.CountAsync();

        // Map to ProductResponse for consistent API
        var productResponses = products.Select(p => new ProductResponse
        {
            Id = p.Id,
            SoftOneId = p.SoftOneId ?? "",
            WooCommerceId = p.WooCommerceId ?? "",
            AtumId = p.AtumId ?? "",
            Name = p.Name ?? "",
            Sku = p.Sku ?? "",
            Price = p.Price,
            Quantity = p.Quantity,
            AtumQuantity = p.AtumQuantity,
            LastSyncedAt = p.LastSyncedAt,
            LastSyncStatus = p.LastSyncStatus ?? "",
            // Add missing fields to ProductResponse
            InternalId = p.InternalId ?? "",
            Barcode = p.Barcode ?? "",
            Category = p.Category ?? "",
            Unit = p.Unit ?? "",
            Group = p.Group ?? "",
            Vat = p.Vat ?? "",
            WholesalePrice = p.WholesalePrice,
            SalePrice = p.SalePrice,
            PurchasePrice = p.PurchasePrice,
            Discount = p.Discount,
            ImageData = p.ImageData ?? "",
            ZoomInfo = p.ZoomInfo ?? "",
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            LastSyncError = p.LastSyncError
        }).ToList();

        return Results.Ok(new ProductsPageResponse
        {
            Products = productResponses,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }
    catch (Exception ex)
    {
        // Log the actual error for debugging
        Console.WriteLine($"Error in products endpoint: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");

        // Return empty response with error
        return Results.Ok(new ProductsPageResponse
        {
            Products = new List<ProductResponse>(),
            Page = page,
            PageSize = pageSize,
            Total = 0,
            TotalPages = 0
        });
    }
})
.WithName("GetProducts")
.WithSummary("Get paginated list of synchronized products")
.WithDescription("Retrieves a paginated list of products that have been synchronized from SoftOne Go");

productsGroup.MapGet("/{id:int}", async (int id, SyncDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    return product != null ? Results.Ok(product) : Results.NotFound();
})
.WithName("GetProduct")
.WithSummary("Get a specific product by ID")
.WithDescription("Retrieves detailed information about a specific product");

storesGroup.MapGet("/", async (SyncDbContext db) =>
{
    var stores = await db.Stores
        .Where(s => s.IsActive)
        .Select(s => new
        {
            s.Id,
            s.Name,
            s.WooCommerceUrl,
            s.IsActive,
            s.LastSyncAt,
            s.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(stores);
})
.WithName("GetStores")
.WithSummary("Get list of active stores")
.WithDescription("Retrieves all active WooCommerce stores configured for synchronization");


// Settings endpoints
settingsGroup.MapGet("/", async (SettingsService settingsService, ILogger<Program> logger) =>
{
    logger.LogDebug("Getting application settings from database");

    try
    {
        var appSettings = await settingsService.GetAppSettingsAsync();
        var settings = appSettings.ToApiModel();

        // Don't expose the actual password in the response
        settings.Email.Password = string.IsNullOrEmpty(appSettings.EmailPassword) ? "" : "***";

        logger.LogDebug("Successfully retrieved settings from database");
        return Results.Ok(settings);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving settings from database");
        return Results.Problem("Failed to retrieve settings");
    }
})
.WithName("GetSettings")
.WithSummary("Get current application settings")
.WithDescription("Retrieves the current configuration settings for all integrated services");

settingsGroup.MapPut("/", async (HttpContext context, SettingsService settingsService, ILogger<Program> logger) =>
{
    logger.LogDebug("Updating application settings in database");

    try
    {
        // Read the raw JSON first for debugging
        using var reader = new StreamReader(context.Request.Body);
        var rawJson = await reader.ReadToEndAsync();
        logger.LogDebug("Received raw JSON: {RawJson}", rawJson);

        // Deserialize manually with more control
        var settings = JsonSerializer.Deserialize<ApiSettingsModel>(rawJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (settings == null)
        {
            logger.LogError("Failed to deserialize settings from JSON");
            return Results.BadRequest("Invalid settings data");
        }

        logger.LogDebug("Successfully deserialized settings for store: {StoreName}", settings.Name);

        // Get existing settings from database
        var appSettings = await settingsService.GetAppSettingsAsync();

        // Update the settings with new values
        appSettings.UpdateFromApiModel(settings);

        // Save to database
        await settingsService.UpdateAppSettingsAsync(appSettings);

        logger.LogInformation("Settings updated successfully in database for store: {StoreName}", settings.Name);
        return Results.Ok(new { message = "Settings updated successfully" });
    }
    catch (JsonException ex)
    {
        logger.LogError(ex, "Error deserializing settings JSON: {Message}", ex.Message);
        return Results.BadRequest("Invalid JSON format");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating settings in database: {Message}", ex.Message);
        return Results.Problem("Failed to update settings");
    }
})
.WithName("UpdateSettings")
.WithSummary("Update application settings")
.WithDescription("Updates the configuration settings for integrated services");

settingsGroup.MapGet("/test/{service}", async (string service, SettingsService settingsService, SoftOneApiService softOneApiService, EmailService emailService, ILogger<Program> logger) =>
{
    logger.LogDebug("Testing connection for service: {Service}", service);

    try
    {
        // Get settings from database
        var appSettings = await settingsService.GetAppSettingsAsync();

        switch (service.ToLower())
        {
            case "softone":
                // Check if required fields are present
                if (string.IsNullOrEmpty(appSettings.SoftOneGoBaseUrl) ||
                    string.IsNullOrEmpty(appSettings.SoftOneGoToken) ||
                    string.IsNullOrEmpty(appSettings.SoftOneGoAppId) ||
                    string.IsNullOrEmpty(appSettings.SoftOneGoS1Code))
                {
                    logger.LogWarning("SoftOne connection test failed - missing configuration");
                    return Results.BadRequest(new { message = "SoftOne connection failed - missing configuration" });
                }

                // Test actual connection to SoftOne API
                logger.LogInformation("Testing actual SoftOne API connection...");
                var softOneResult = await softOneApiService.TestConnectionAsync(
                    appSettings.SoftOneGoBaseUrl,
                    appSettings.SoftOneGoAppId,
                    appSettings.SoftOneGoToken,
                    appSettings.SoftOneGoS1Code
                );

                if (softOneResult)
                {
                    logger.LogInformation("SoftOne API connection test successful");
                    return Results.Ok(new { message = "SoftOne connection successful" });
                }
                else
                {
                    logger.LogWarning("SoftOne API connection test failed");
                    return Results.BadRequest(new { message = "SoftOne connection failed - check credentials and network" });
                }

            case "woocommerce":
                // Simulate WooCommerce test (can be implemented later)
                await Task.Delay(500);
                var wooValid = !string.IsNullOrEmpty(appSettings.WooCommerceUrl) && !string.IsNullOrEmpty(appSettings.WooCommerceConsumerKey);
                if (wooValid)
                {
                    logger.LogInformation("WooCommerce connection test successful (simulated)");
                    return Results.Ok(new { message = "WooCommerce connection successful" });
                }
                else
                {
                    logger.LogWarning("WooCommerce connection test failed - missing configuration");
                    return Results.BadRequest(new { message = "WooCommerce connection failed - missing configuration" });
                }

            case "atum":
                // Simulate ATUM test (can be implemented later)
                await Task.Delay(500);
                var atumValid = appSettings.AtumLocationId > 0;
                if (atumValid)
                {
                    logger.LogInformation("ATUM connection test successful (simulated)");
                    return Results.Ok(new { message = "ATUM connection successful" });
                }
                else
                {
                    logger.LogWarning("ATUM connection test failed - missing configuration");
                    return Results.BadRequest(new { message = "ATUM connection failed - missing configuration" });
                }

            case "email":
                // Test email settings
                logger.LogInformation("Testing email configuration...");

                if (string.IsNullOrEmpty(appSettings.EmailSmtpHost) ||
                    string.IsNullOrEmpty(appSettings.EmailFromEmail) ||
                    string.IsNullOrEmpty(appSettings.EmailToEmail))
                {
                    logger.LogWarning("Email test failed - missing configuration");
                    return Results.BadRequest(new { message = "Email configuration incomplete - please fill in SMTP host, from email, and to email" });
                }

                try
                {
                    // Use the EmailService to send an actual test email
                    var emailResult = await emailService.TestEmailSettingsAsync(appSettings);

                    if (emailResult)
                    {
                        logger.LogInformation("Test email sent successfully to {ToEmail}", appSettings.EmailToEmail);
                        return Results.Ok(new { message = $"Test email sent successfully to {appSettings.EmailToEmail}" });
                    }
                    else
                    {
                        logger.LogWarning("Test email failed to send");
                        return Results.BadRequest(new { message = "Failed to send test email. Please check your SMTP settings." });
                    }
                }
                catch (Exception emailEx)
                {
                    logger.LogError(emailEx, "Email test failed: {Message}", emailEx.Message);
                    return Results.BadRequest(new { message = $"Email test failed: {emailEx.Message}" });
                }

            default:
                logger.LogWarning("Unknown service for connection test: {Service}", service);
                return Results.BadRequest(new { message = $"Unknown service: {service}" });
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error testing {Service} connection: {Message}", service, ex.Message);
        return Results.Problem($"Error testing {service} connection: {ex.Message}");
    }
})
.WithName("TestConnection")
.WithSummary("Test connection to external services")
.WithDescription("Tests the connection and authentication for the specified external service (softone, woocommerce, atum, email)");

settingsGroup.MapGet("/export", async (SettingsService settingsService, ILogger<Program> logger) =>
{
    logger.LogInformation("Exporting settings configuration for Windows Service");

    try
    {
        var appSettings = await settingsService.GetAppSettingsAsync();

        // Create configuration object for Windows Service
        var exportConfig = new
        {
            SoftOne = new
            {
                BaseUrl = appSettings.SoftOneGoBaseUrl,
                Token = appSettings.SoftOneGoToken,
                AppId = appSettings.SoftOneGoAppId,
                S1Code = appSettings.SoftOneGoS1Code
            },
            WooCommerce = new
            {
                ConsumerKey = appSettings.WooCommerceConsumerKey,
                ConsumerSecret = appSettings.WooCommerceConsumerSecret
            },
            ATUM = new
            {
                LocationId = appSettings.AtumLocationId,
                LocationName = appSettings.AtumLocationName
            },
            Email = new
            {
                SmtpHost = appSettings.EmailSmtpHost,
                SmtpPort = appSettings.EmailSmtpPort,
                Username = appSettings.EmailUsername,
                Password = appSettings.EmailPassword, // Include actual password for service
                FromEmail = appSettings.EmailFromEmail,
                ToEmail = appSettings.EmailToEmail,
                EnableNotifications = appSettings.SyncEmailNotifications
            },
            SyncSettings = new
            {
                IntervalMinutes = 10, // Default sync interval
                EnableAutoSync = true,
                BatchSize = 50
            }
        };

        var json = JsonSerializer.Serialize(exportConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"appsettings.Soft1ToAtum.{DateTime.Now:yyyyMMdd-HHmmss}.json";

        logger.LogInformation("Settings exported successfully to {FileName}", fileName);

        return Results.File(bytes, "application/json", fileName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error exporting settings: {Message}", ex.Message);
        return Results.Problem("Failed to export settings");
    }
})
.WithName("ExportSettings")
.WithSummary("Export settings configuration for Windows Service")
.WithDescription("Exports current settings as a JSON file that can be used by the Windows Service");

// Add endpoint for getting product statistics by source
productsGroup.MapGet("/statistics", async (SyncDbContext db, ILogger<Program> logger) =>
{
    try
    {
        logger.LogDebug("Getting product statistics by source");

        var totalProducts = await db.Products.CountAsync();

        // Count products by source (based on which external IDs they have)
        var softOneProducts = await db.Products.CountAsync(p => !string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId));
        var atumProducts = await db.Products.CountAsync(p => !string.IsNullOrEmpty(p.AtumId));
        var wooCommerceProducts = await db.Products.CountAsync(p => !string.IsNullOrEmpty(p.WooCommerceId));

        // Count products by sync status
        var createdProducts = await db.Products.CountAsync(p => p.LastSyncStatus == "Created");
        var updatedProducts = await db.Products.CountAsync(p => p.LastSyncStatus == "Updated");
        var errorProducts = await db.Products.CountAsync(p => p.LastSyncStatus == "Error" || p.LastSyncStatus == "Failed");
        var skippedProducts = await db.Products.CountAsync(p => p.LastSyncStatus == "Skipped");

        // Calculate products with data from multiple sources
        var softOneAndAtum = await db.Products.CountAsync(p =>
            (!string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId)) &&
            !string.IsNullOrEmpty(p.AtumId));
        var softOneAndWooCommerce = await db.Products.CountAsync(p =>
            (!string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId)) &&
            !string.IsNullOrEmpty(p.WooCommerceId));
        var atumAndWooCommerce = await db.Products.CountAsync(p =>
            !string.IsNullOrEmpty(p.AtumId) && !string.IsNullOrEmpty(p.WooCommerceId));
        var allThreeSources = await db.Products.CountAsync(p =>
            (!string.IsNullOrEmpty(p.SoftOneId) || !string.IsNullOrEmpty(p.InternalId)) &&
            !string.IsNullOrEmpty(p.AtumId) && !string.IsNullOrEmpty(p.WooCommerceId));

        var statistics = new
        {
            Total = totalProducts,
            BySources = new
            {
                SoftOne = softOneProducts,
                ATUM = atumProducts,
                WooCommerce = wooCommerceProducts
            },
            ByStatus = new
            {
                Created = createdProducts,
                Updated = updatedProducts,
                Error = errorProducts,
                Skipped = skippedProducts
            },
            Integration = new
            {
                SoftOneAndATUM = softOneAndAtum,
                SoftOneAndWooCommerce = softOneAndWooCommerce,
                ATUMAndWooCommerce = atumAndWooCommerce,
                AllThreeSources = allThreeSources
            }
        };

        logger.LogDebug("Product statistics retrieved successfully: Total={Total}, SoftOne={SoftOne}, ATUM={ATUM}, WooCommerce={WooCommerce}",
            totalProducts, softOneProducts, atumProducts, wooCommerceProducts);

        return Results.Ok(statistics);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting product statistics: {Message}", ex.Message);
        return Results.Problem($"Error getting product statistics: {ex.Message}");
    }
})
.WithName("GetProductStatistics")
.WithSummary("Get detailed product statistics by source")
.WithDescription("Retrieves comprehensive statistics about products broken down by source system (SoftOne, ATUM, WooCommerce) and sync status");

// Add delete all products endpoint for testing purposes
productsGroup.MapDelete("/all", async (SyncDbContext db, ILogger<Program> logger) =>
{
    try
    {
        logger.LogWarning("DELETE ALL PRODUCTS requested - this will delete all products from the database");

        var productsCount = await db.Products.CountAsync();
        logger.LogInformation("Found {ProductCount} products to delete", productsCount);

        if (productsCount == 0)
        {
            return Results.Ok(new { message = "No products found to delete", deletedCount = 0 });
        }

        // Delete all products
        db.Products.RemoveRange(db.Products);
        await db.SaveChangesAsync();

        logger.LogWarning("Successfully deleted {ProductCount} products from database", productsCount);

        return Results.Ok(new {
            message = $"Successfully deleted all {productsCount} products",
            deletedCount = productsCount
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting all products: {Message}", ex.Message);
        return Results.Problem($"Error deleting products: {ex.Message}");
    }
})
.WithName("DeleteAllProducts")
.WithSummary("Delete all products from database")
.WithDescription("⚠️ DANGER: This will permanently delete ALL products from the database. Use only for testing!");

app.MapDefaultEndpoints();

app.Run();
