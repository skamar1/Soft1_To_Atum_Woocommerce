using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Soft1_To_Atum.ApiService.Extensions;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
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
builder.Services.AddScoped<Soft1_To_Atum.ApiService.Services.AtumBatchService>();
builder.Services.AddHttpClient();

// Configure specific HttpClient for WooCommerce with longer timeout
builder.Services.AddHttpClient<WooCommerceApiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes timeout for WooCommerce API
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

syncGroup.MapPost("/manual", async (SyncDbContext db, SettingsService settingsService, SoftOneApiService softOneApiService, ProductMatchingService productMatchingService, EmailService emailService, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    Console.WriteLine("\n\nManual sync endpoint called\n\n");
    logger.LogInformation("===== MANUAL SYNC ENDPOINT CALLED =====");
    logger.LogInformation("Manual sync requested at {time}", DateTime.UtcNow);

    //get settings from database
    var settings = await settingsService.GetAppSettingsAsync();

    // Check if settings are valid
    if (settings == null)
    {
        logger.LogError("Failed to retrieve settings");
        return Results.Problem("Failed to retrieve settings");
    }

    logger.LogDebug("Retrieved settings from database");

    //deserialize to api model for easier handling
    var apiSettings = settings.ToApiModel();

    if (string.IsNullOrEmpty(apiSettings.SoftOneGo.Token))
    {
        logger.LogError("SoftOne Go token is not configured");
        return Results.Problem("SoftOne Go is not configured go to settings to configure the service.");
    }

    var client = new HttpClient();
    var request = new HttpRequestMessage(HttpMethod.Post, $"{apiSettings.SoftOneGo.BaseUrl}/list/item");
    request.Headers.Add("s1code", apiSettings.SoftOneGo.S1Code);
    var content = new StringContent($"{{\n    \"appId\": \"703\",\n    \"filters\": \"ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999\",\n    \"token\": \"{apiSettings.SoftOneGo.Token}\"\n}}", null, "application/json");
    request.Content = content;

    Console.WriteLine("Sending request to SoftOne Go API...");

    var response = await client.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        logger.LogError("SoftOne API returned error {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
        logger.LogError("Request URL: {RequestUrl}", request.RequestUri);
        logger.LogError("Request Headers: s1code={S1Code}", apiSettings.SoftOneGo.S1Code);

        return Results.Problem($"SoftOne API error: {response.StatusCode} - {errorContent}");
    }


    // Register encoding provider for Windows-1253
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    
    // Check if response is compressed and handle accordingly
    string responseContent;
    var contentEncoding = response.Content.Headers.ContentEncoding;
    
    if (contentEncoding.Contains("gzip"))
    {
        // Handle gzip compressed response
        using var responseStream = await response.Content.ReadAsStreamAsync();
        using var gzipStream = new System.IO.Compression.GZipStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, Encoding.GetEncoding("windows-1253"));
        responseContent = await reader.ReadToEndAsync();
    }
    else
    {
        // Handle uncompressed response with Windows-1253 encoding
        var responseBytes = await response.Content.ReadAsByteArrayAsync();
        var encoding = Encoding.GetEncoding("windows-1253");
        responseContent = encoding.GetString(responseBytes);
    }

    Console.WriteLine("Received response from SoftOne Go API...");

    try
    {
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;

        if (root.TryGetProperty("success", out var successElement) && successElement.GetBoolean())
        {
            var totalCount = root.TryGetProperty("totalcount", out var totalElement) ? totalElement.GetInt32() : 0;
            var updateDate = root.TryGetProperty("upddate", out var updateElement) ? updateElement.GetString() : "";
            var requestId = root.TryGetProperty("reqID", out var reqElement) ? reqElement.GetString() : "";
            
            Console.WriteLine($"SoftOne API Success! Total Products: {totalCount}");
            Console.WriteLine($"Update Date: {updateDate}");
            Console.WriteLine($"Request ID: {requestId}");
            
            if (root.TryGetProperty("fields", out var fieldsElement) && 
                root.TryGetProperty("rows", out var rowsElement))
            {
                // Extract field names
                var fieldNames = new List<string>();
                foreach (var field in fieldsElement.EnumerateArray())
                {
                    if (field.TryGetProperty("name", out var nameElement))
                    {
                        fieldNames.Add(nameElement.GetString() ?? "");
                    }
                }
                
                // Convert rows to structured products
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
                
                Console.WriteLine($"Successfully parsed {products.Count} products");
                
                // Display first few products for debugging
                foreach (var product in products.Take(5))
                {
                    var name = product.GetValueOrDefault("ITEM.NAME") ?? "";
                    var code = product.GetValueOrDefault("ITEM.CODE") ?? "";
                    var price = product.GetValueOrDefault("ITEM.PRICER") ?? "";
                    var stock = product.GetValueOrDefault("ITEM.MTRL_ITEMTRDATA_QTY1") ?? "";
                    
                    Console.WriteLine($"Product: {name} | Code: {code} | Price: €{price} | Stock: {stock}");
                }
                
                // Process products using ProductMatchingService
                logger.LogInformation("Retrieved {ProductCount} products from SoftOne Go API, starting database sync...", products.Count);

                // Create sync log
                var syncLog = new SyncLog
                {
                    StartedAt = DateTime.UtcNow,
                    Status = "Running",
                    TotalProducts = products.Count,
                    CreatedProducts = 0,
                    UpdatedProducts = 0,
                    SkippedProducts = 0,
                    ErrorCount = 0
                };

                db.SyncLogs.Add(syncLog);
                await db.SaveChangesAsync(cancellationToken);

                var createdCount = 0;
                var updatedCount = 0;
                var errorCount = 0;

                // Process each product using ProductMatchingService
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

                            logger.LogDebug("Processed product {ProductId}: {Action} via {MatchType} match",
                                result.Product.Id, result.Action, result.MatchType);
                        }
                        else
                        {
                            errorCount++;
                            logger.LogWarning("Failed to process SoftOne product: {ErrorMessage}", result.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var productName = product.GetValueOrDefault("ITEM.NAME") ?? "Unknown";
                        logger.LogError(ex, "Error processing SoftOne product {ProductName}: {Message}",
                            productName, ex.Message);
                    }
                }

                // Update sync log with results
                syncLog.CreatedProducts = createdCount;
                syncLog.UpdatedProducts = updatedCount;
                syncLog.ErrorCount = errorCount;
                syncLog.Status = errorCount > 0 ? "Completed with errors" : "Completed";
                syncLog.CompletedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Manual sync completed. Created: {Created}, Updated: {Updated}, Errors: {Errors}",
                    createdCount, updatedCount, errorCount);

                return Results.Ok(new ManualSyncResponse
                {
                    Message = $"Manual sync completed successfully. Created: {createdCount}, Updated: {updatedCount}, Errors: {errorCount}",
                    SyncLogId = syncLog.Id
                });
            }
            else
            {
                Console.WriteLine("Missing fields or rows in SoftOne API response");
                return Results.Problem("Missing fields or rows in SoftOne API response");
            }
        }
        else
        {
            Console.WriteLine("SoftOne API call was not successful");
            logger.LogWarning("SoftOne API returned success=false");
            return Results.Problem("SoftOne API returned unsuccessful response");
        }
    }
    catch (JsonException jsonEx)
    {
        logger.LogError(jsonEx, "Error deserializing SoftOne response: {Message}", jsonEx.Message);
        Console.WriteLine($"Error deserializing SoftOne response: {jsonEx.Message}");
        Console.WriteLine("Raw response content for debugging:");
        Console.WriteLine(responseContent.Substring(0, Math.Min(500, responseContent.Length)));
        return Results.Problem("Error deserializing SoftOne response");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error processing SoftOne response: {Message}", ex.Message);
        Console.WriteLine($"Unexpected error processing SoftOne response: {ex.Message}");
        return Results.Problem("Unexpected error processing SoftOne response");
    }
    






















































   
})
.WithName("StartManualSync")
.WithSummary("Start a manual synchronization process")
.WithDescription("Initiates a manual synchronization of products from SoftOne Go to WooCommerce ATUM");

syncGroup.MapPost("/atum", async (SyncDbContext db, SettingsService settingsService, AtumApiService atumApiService, ProductMatchingService productMatchingService, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation("ATUM sync requested at {time}", DateTime.UtcNow);

    // Get settings from database
    var settings = await settingsService.GetAppSettingsAsync();

    // Check if settings are valid
    if (settings == null)
    {
        logger.LogError("Failed to retrieve settings");
        return Results.Problem("Failed to retrieve settings");
    }

    // Deserialize to API model for easier handling
    var apiSettings = settings.ToApiModel();

    if (string.IsNullOrEmpty(apiSettings.WooCommerce.ConsumerKey) || string.IsNullOrEmpty(apiSettings.WooCommerce.ConsumerSecret))
    {
        logger.LogError("WooCommerce credentials are not configured");
        return Results.Problem("WooCommerce credentials are not configured. Go to settings to configure the service.");
    }

    try
    {
        logger.LogInformation("Starting ATUM inventory fetch for location {LocationId}", apiSettings.ATUM.LocationId);

        // Fetch all ATUM inventory items
        var atumItems = await atumApiService.GetAllInventoryAsync(
            apiSettings.WooCommerce.ConsumerKey,
            apiSettings.WooCommerce.ConsumerSecret,
            apiSettings.ATUM.LocationId,
            cancellationToken);

        logger.LogInformation("Retrieved {ItemCount} items from ATUM API, starting database sync...", atumItems.Count);

        // Create sync log
        var syncLog = new SyncLog
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            TotalProducts = atumItems.Count,
            CreatedProducts = 0,
            UpdatedProducts = 0,
            SkippedProducts = 0,
            ErrorCount = 0
        };

        db.SyncLogs.Add(syncLog);
        await db.SaveChangesAsync(cancellationToken);

        var createdCount = 0;
        var updatedCount = 0;
        var errorCount = 0;

        // Process each ATUM item using ProductMatchingService
        foreach (var atumItem in atumItems)
        {
            try
            {
                var result = await productMatchingService.ProcessAtumProductAsync(atumItem, cancellationToken);

                if (result.Success)
                {
                    if (result.Action == ProductAction.Created)
                        createdCount++;
                    else if (result.Action == ProductAction.Updated)
                        updatedCount++;

                    logger.LogDebug("Processed ATUM product {ProductId}: {Action} via {MatchType} match",
                        result.Product.Id, result.Action, result.MatchType);
                }
                else
                {
                    errorCount++;
                    logger.LogWarning("Failed to process ATUM product: {ErrorMessage}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                logger.LogError(ex, "Error processing ATUM product {AtumId} ({SKU}): {Message}",
                    atumItem.Id, atumItem.GetSku(), ex.Message);
            }
        }

        // Update sync log with results
        syncLog.CreatedProducts = createdCount;
        syncLog.UpdatedProducts = updatedCount;
        syncLog.ErrorCount = errorCount;
        syncLog.Status = errorCount > 0 ? "Completed with errors" : "Completed";
        syncLog.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ATUM sync completed. Created: {Created}, Updated: {Updated}, Errors: {Errors}",
            createdCount, updatedCount, errorCount);

        return Results.Ok(new ManualSyncResponse
        {
            Message = $"ATUM sync completed successfully. Created: {createdCount}, Updated: {updatedCount}, Errors: {errorCount}",
            SyncLogId = syncLog.Id
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during ATUM sync: {Message}", ex.Message);
        return Results.Problem($"Error during ATUM sync: {ex.Message}");
    }
})
.WithName("StartAtumSync")
.WithSummary("Start an ATUM inventory synchronization")
.WithDescription("Initiates a synchronization of inventory data from ATUM Multi Inventory");

syncGroup.MapPost("/atum-batch", async (SyncDbContext db, SettingsService settingsService, AtumApiService atumApiService, Soft1_To_Atum.ApiService.Services.AtumBatchService atumBatchService, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation("=== ATUM BATCH SYNC REQUESTED ===");
    logger.LogInformation("ATUM batch sync requested at {time}", DateTime.UtcNow);

    try
    {
        // Get settings from database
        var settings = await settingsService.GetAppSettingsAsync();
        if (settings == null)
        {
            logger.LogError("Settings not found");
            return Results.Problem("Settings not configured");
        }

        if (string.IsNullOrEmpty(settings.WooCommerceConsumerKey) || string.IsNullOrEmpty(settings.WooCommerceConsumerSecret))
        {
            logger.LogError("WooCommerce API credentials not configured");
            return Results.Problem("WooCommerce API credentials not configured");
        }

        // Prepare batch request using AtumBatchService (limit to 50 items to avoid "Request Entity Too Large")
        logger.LogInformation("Preparing ATUM batch request...");
        var batchRequest = await atumBatchService.PrepareAtumBatchRequestAsync(maxBatchSize: 50);

        if ((batchRequest.Create?.Count ?? 0) == 0 && (batchRequest.Update?.Count ?? 0) == 0)
        {
            logger.LogInformation("No products need batch synchronization");
            return Results.Ok(new ManualSyncResponse
            {
                Message = "No products need batch synchronization. All products are already in sync.",
                SyncLogId = 0
            });
        }

        // Create sync log
        var syncLog = new SyncLog
        {
            StartedAt = DateTime.UtcNow,
            Status = "Running",
            TotalProducts = (batchRequest.Create?.Count ?? 0) + (batchRequest.Update?.Count ?? 0),
            CreatedProducts = 0,
            UpdatedProducts = 0,
            SkippedProducts = 0,
            ErrorCount = 0
        };

        db.SyncLogs.Add(syncLog);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created sync log with ID: {SyncLogId}", syncLog.Id);

        // Perform batch update via ATUM API
        logger.LogInformation("Sending batch request to ATUM API...");
        var batchResponse = await atumApiService.BatchUpdateInventoryAsync(
            settings.WooCommerceConsumerKey,
            settings.WooCommerceConsumerSecret,
            batchRequest,
            cancellationToken);

        // Process the batch response
        logger.LogInformation("Processing ATUM batch response...");
        await atumBatchService.ProcessAtumBatchResponseAsync(batchResponse);

        // Update sync log with results
        syncLog.CompletedAt = DateTime.UtcNow;
        syncLog.Status = "Completed";
        syncLog.CreatedProducts = batchResponse.Create?.Count(p => p.Id > 0) ?? 0;
        syncLog.UpdatedProducts = batchResponse.Update?.Count(p => p.Id > 0) ?? 0;
        syncLog.ErrorCount = (batchResponse.Create?.Count(p => !string.IsNullOrEmpty(p.Error?.Message)) ?? 0) +
                            (batchResponse.Update?.Count(p => !string.IsNullOrEmpty(p.Error?.Message)) ?? 0);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("=== ATUM BATCH SYNC COMPLETED ===");
        logger.LogInformation("Batch sync results: {CreatedCount} created, {UpdatedCount} updated, {ErrorCount} errors",
            syncLog.CreatedProducts, syncLog.UpdatedProducts, syncLog.ErrorCount);

        return Results.Ok(new ManualSyncResponse
        {
            Message = $"ATUM batch sync completed successfully. Created: {syncLog.CreatedProducts}, Updated: {syncLog.UpdatedProducts}, Errors: {syncLog.ErrorCount}",
            SyncLogId = syncLog.Id
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during ATUM batch sync: {Message}", ex.Message);
        return Results.Problem($"Error during ATUM batch sync: {ex.Message}");
    }
})
.WithName("StartAtumBatchSync")
.WithSummary("Start an ATUM batch inventory synchronization")
.WithDescription("Initiates a batch synchronization to create/update products in ATUM based on SoftOne quantities");

// WooCommerce sync endpoint
syncGroup.MapPost("/woocommerce", async (SyncDbContext db, SettingsService settingsService, WooCommerceApiService wooCommerceApiService, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    logger.LogInformation("=== WOOCOMMERCE SYNC REQUESTED ===");
    logger.LogInformation("WooCommerce sync requested at {time}", DateTime.UtcNow);

    try
    {
        // Get settings from database
        var settings = await settingsService.GetAppSettingsAsync();
        if (settings == null)
        {
            logger.LogError("Settings not found");
            return Results.Problem("Settings not configured");
        }

        if (string.IsNullOrEmpty(settings.WooCommerceConsumerKey) || string.IsNullOrEmpty(settings.WooCommerceConsumerSecret))
        {
            logger.LogError("WooCommerce API credentials not configured");
            return Results.Problem("WooCommerce API credentials not configured");
        }

        logger.LogInformation("Starting WooCommerce product sync...");

        // Fetch all products from WooCommerce
        var wooCommerceProducts = await wooCommerceApiService.GetAllProductsAsync(
            settings.WooCommerceConsumerKey,
            settings.WooCommerceConsumerSecret,
            cancellationToken);

        logger.LogInformation("Retrieved {count} products from WooCommerce", wooCommerceProducts.Count);

        // Update database with WooCommerce IDs
        int matchedProducts = 0;
        int createdProducts = 0;
        int errorCount = 0;

        foreach (var wooProduct in wooCommerceProducts)
        {
            try
            {
                if (string.IsNullOrEmpty(wooProduct.Sku))
                {
                    logger.LogWarning("WooCommerce product {id} '{name}' has no SKU, skipping", wooProduct.Id, wooProduct.Name);
                    continue;
                }

                // Try to find existing product by SKU
                var existingProduct = await db.Products
                    .FirstOrDefaultAsync(p => p.Sku == wooProduct.Sku, cancellationToken);

                if (existingProduct != null)
                {
                    // Update existing product with WooCommerce ID and name if missing
                    existingProduct.WooCommerceId = wooProduct.Id.ToString();
                    if (string.IsNullOrEmpty(existingProduct.Name))
                    {
                        existingProduct.Name = wooProduct.Name;
                    }
                    existingProduct.LastSyncedAt = DateTime.UtcNow;
                    existingProduct.LastSyncStatus = "WooCommerce Synced";

                    logger.LogDebug("Updated existing product {sku} with WooCommerce ID {id}", wooProduct.Sku, wooProduct.Id);
                    matchedProducts++;
                }
                else
                {
                    // Create new product entry for WooCommerce-only product
                    var newProduct = new Product
                    {
                        Sku = wooProduct.Sku,
                        Name = wooProduct.Name,
                        WooCommerceId = wooProduct.Id.ToString(),
                        Quantity = 0, // Default quantity for WooCommerce-only products
                        AtumQuantity = 0,
                        LastSyncedAt = DateTime.UtcNow,
                        LastSyncStatus = "WooCommerce Synced"
                    };

                    db.Products.Add(newProduct);
                    logger.LogDebug("Created new product entry for WooCommerce product {sku} (ID: {id})", wooProduct.Sku, wooProduct.Id);
                    createdProducts++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing WooCommerce product {id} ({sku})", wooProduct.Id, wooProduct.Sku);
                errorCount++;
            }
        }

        // Save all changes to database
        var savedChanges = await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("=== WOOCOMMERCE SYNC COMPLETED ===");
        logger.LogInformation("Results: {matched} matched, {created} created, {errors} errors, {saved} database changes",
            matchedProducts, createdProducts, errorCount, savedChanges);

        return Results.Ok(new
        {
            Success = true,
            Message = "WooCommerce sync completed successfully",
            TotalProducts = wooCommerceProducts.Count,
            MatchedProducts = matchedProducts,
            CreatedProducts = createdProducts,
            ErrorCount = errorCount,
            DatabaseChanges = savedChanges
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during WooCommerce sync");
        return Results.Problem($"WooCommerce sync failed: {ex.Message}");
    }
})
.WithName("StartWooCommerceSync")
.WithSummary("Start a WooCommerce product synchronization")
.WithDescription("Fetches all products from WooCommerce and updates the local database with WooCommerce IDs and product information");

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
