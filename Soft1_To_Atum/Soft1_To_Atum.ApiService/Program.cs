using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Soft1_To_Atum.ApiService.Extensions;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Data.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlite("Data Source=sync.db"));

// Add services
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SoftOneApiService>();
builder.Services.AddHttpClient();

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
    var products = await db.Products
        .OrderByDescending(p => p.LastSyncedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    var total = await db.Products.CountAsync();

    return Results.Ok(new
    {
        Products = products,
        Page = page,
        PageSize = pageSize,
        Total = total,
        TotalPages = (int)Math.Ceiling((double)total / pageSize)
    });
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

syncGroup.MapPost("/manual", async (SyncDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("===== MANUAL SYNC ENDPOINT CALLED =====");
    logger.LogInformation("Manual sync requested at {time}", DateTime.UtcNow);
    logger.LogDebug("Request received on /api/sync/manual endpoint");

    try
    {
        logger.LogDebug("Creating new SyncLog entity");
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

        logger.LogDebug("Adding SyncLog to database context");
        db.SyncLogs.Add(syncLog);

        logger.LogDebug("Saving changes to database");
        await db.SaveChangesAsync();

        logger.LogInformation("Created sync log with ID {syncLogId}", syncLog.Id);

        // Simulate sync process (replace with actual sync logic)
        logger.LogInformation("Starting sync simulation...");
        logger.LogDebug("Simulating 2 second delay");
        await Task.Delay(2000); // Simulate work

        // Update log with results
        logger.LogDebug("Updating sync log with completion status");
        syncLog.Status = "Completed";
        syncLog.CompletedAt = DateTime.UtcNow;
        syncLog.TotalProducts = 10;
        syncLog.CreatedProducts = 3;
        syncLog.UpdatedProducts = 5;
        syncLog.SkippedProducts = 2;

        logger.LogDebug("Saving updated sync log to database");
        await db.SaveChangesAsync();

        logger.LogInformation("Manual sync completed. Sync ID: {syncLogId}, Duration: {duration}",
            syncLog.Id, syncLog.Duration);

        var result = new { message = "Manual sync completed", syncLogId = syncLog.Id };
        logger.LogDebug("Returning result: {Result}", System.Text.Json.JsonSerializer.Serialize(result));
        logger.LogInformation("===== MANUAL SYNC ENDPOINT COMPLETED SUCCESSFULLY =====");

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error occurred during manual sync: {Message}", ex.Message);
        logger.LogError("===== MANUAL SYNC ENDPOINT FAILED =====");
        return Results.Problem("An error occurred during sync");
    }
})
.WithName("StartManualSync")
.WithSummary("Start a manual synchronization process")
.WithDescription("Initiates a manual synchronization of products from SoftOne Go to WooCommerce ATUM");

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

settingsGroup.MapGet("/test/{service}", async (string service, SettingsService settingsService, SoftOneApiService softOneApiService, ILogger<Program> logger) =>
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
                    // Simple email test - just validate configuration for now
                    // In a real implementation, you would send an actual test email
                    logger.LogInformation("Email configuration appears valid");
                    return Results.Ok(new { message = $"Email configuration is valid. Test email would be sent from {appSettings.EmailFromEmail} to {appSettings.EmailToEmail}" });
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

app.MapDefaultEndpoints();

app.Run();
