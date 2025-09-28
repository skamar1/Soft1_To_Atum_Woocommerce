using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Soft1_To_Atum.Data;
using Soft1_To_Atum.Web;
using Soft1_To_Atum.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add database context
builder.Services.AddDbContext<SyncDbContext>(options =>
    options.UseSqlite("Data Source=sync.db"));

// Add HTTP client for API calls using configuration
builder.Services.AddHttpClient<SyncApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7463/";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60); // 1 minute timeout for most operations
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => "Healthy");
app.MapDefaultEndpoints();

app.Run();
