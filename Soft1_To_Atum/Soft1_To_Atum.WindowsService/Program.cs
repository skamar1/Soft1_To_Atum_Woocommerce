using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.EventLog;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Services;
using Soft1_To_Atum.WindowsService;

var builder = Host.CreateApplicationBuilder(args);

// Configure Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Soft1ToAtumSyncService";
});

// Add Event Log logging for Windows
builder.Services.Configure<EventLogSettings>(options =>
{
    options.SourceName = "Soft1ToAtumSyncService";
    options.LogName = "Application";
});

// Add console logging for development
builder.Logging.AddConsole();
builder.Logging.AddEventLog();

// Add SQLite Database - use same database as API Service
var dbPath = Path.Combine(AppContext.BaseDirectory, "sync.db");
builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add Settings Services to read from database
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<StoreSettingsService>();

// Add API Services (for backward compatibility, if needed)
builder.Services.AddScoped<SoftOneApiService>();
builder.Services.AddScoped<WooCommerceApiService>();
builder.Services.AddScoped<AtumApiService>();
builder.Services.AddScoped<ProductMatchingService>();
builder.Services.AddScoped<EmailService>();

// Add HttpClient for API calls
builder.Services.AddHttpClient();

// Add HttpClient with long timeout for WooCommerce
builder.Services.AddHttpClient<WooCommerceApiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(20);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    ConnectTimeout = TimeSpan.FromSeconds(30)
});

// Add other HttpClients
builder.Services.AddHttpClient<SoftOneApiService>();
builder.Services.AddHttpClient<AtumApiService>();

// Add the Background Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure database is created
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SyncDbContext>();
    db.Database.EnsureCreated();
}

host.Run();
