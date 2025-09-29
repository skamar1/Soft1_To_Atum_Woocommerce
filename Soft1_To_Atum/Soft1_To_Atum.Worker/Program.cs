using Microsoft.EntityFrameworkCore;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Data.Services;
using Soft1_To_Atum.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlite("Data Source=sync.db"));

builder.Services.AddHttpClient();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<SoftOneApiService>();
builder.Services.AddScoped<AtumApiService>();
builder.Services.AddScoped<WooCommerceApiService>();
builder.Services.AddScoped<ProductMatchingService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<Soft1_To_Atum.Data.Services.DatabaseService>();
builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
host.Run();
