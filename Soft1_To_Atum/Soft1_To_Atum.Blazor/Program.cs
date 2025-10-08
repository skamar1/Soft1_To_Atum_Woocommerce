using MudBlazor.Services;
using Soft1_To_Atum.Blazor.Components;
using Soft1_To_Atum.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add only the services we need from ServiceDefaults, WITHOUT Service Discovery
// because we don't need service discovery for a fixed localhost URL
// and it adds unwanted resilience handlers with default timeouts
builder.ConfigureOpenTelemetry();
builder.AddDefaultHealthChecks();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add HTTP client for API communication
// NOTE: NO resilience handler, NO service discovery - just a plain HttpClient with a very long timeout
// The API service has its own resilience policies and handles all retries/timeouts
builder.Services.AddHttpClient<SyncApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5465"); // ApiService URL - fixed, no service discovery needed
    client.Timeout = TimeSpan.FromMinutes(20); // Very long timeout - let the API service handle everything
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.MapDefaultEndpoints();

app.Run();
