using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Blazor.Services;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Settings : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<Settings> Logger { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ApiSettingsModel? globalSettings;
    private StoreSettingsApiModel? storeSettings;
    private List<StoreResponse>? stores;
    private int selectedStoreId = 1;

    private bool isSaving = false;
    private bool isTestingConnection = false;
    private bool isTestingEmail = false;
    private bool isExporting = false;
    private string testingService = string.Empty;

    protected override void OnInitialized()
    {
        Logger.LogInformation("Settings page OnInitialized called");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Logger.LogInformation("Settings page first render completed");
            Console.WriteLine("Settings page first render completed");
            await LoadSettings();
            StateHasChanged();
        }
    }

    private async Task LoadSettings()
    {
        Console.WriteLine("=== LOAD SETTINGS INITIATED ===");
        Logger.LogInformation("=== LOAD SETTINGS INITIATED ===");
        var maxRetries = 3;
        var delay = TimeSpan.FromSeconds(2);

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                Console.WriteLine("Loading settings...");
                Logger.LogDebug("Loading settings (attempt {Attempt}/{MaxRetries})", retry + 1, maxRetries);

                // Load global settings
                globalSettings = await SyncApi.GetSettingsAsync();
                if (globalSettings == null)
                {
                    globalSettings = new ApiSettingsModel();
                }

                // Load stores
                stores = await SyncApi.GetStoresAsync();
                if (stores == null || stores.Count == 0)
                {
                    Logger.LogWarning("No stores found");
                    Snackbar.Add("No stores configured. Please create a store first at Stores page.", Severity.Warning);
                    stores = new List<StoreResponse>();
                    // Create empty store settings to prevent UI errors
                    storeSettings = new StoreSettingsApiModel
                    {
                        Id = 0,
                        Name = "",
                        Enabled = true,
                        SoftOneGo = new SoftOneGoSettings(),
                        ATUM = new AtumSettings()
                    };
                }
                else
                {
                    // Set selected store to first store
                    selectedStoreId = stores[0].Id;
                    await LoadStoreSettings(selectedStoreId);
                }

                Logger.LogDebug("Settings loaded successfully");
                Console.WriteLine("Settings loaded successfully");
                return; // Success, exit the retry loop
            }
            catch (HttpRequestException ex) when (retry < maxRetries - 1)
            {
                Logger.LogWarning("Settings load attempt {Attempt} failed: {Message}. Retrying in {Delay}ms...",
                    retry + 1, ex.Message, delay.TotalMilliseconds);
                Console.WriteLine($"Settings load attempt {retry + 1} failed: {ex.Message}. Retrying in {delay.TotalMilliseconds}ms...");

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 10)); // Exponential backoff, max 10s
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading settings on attempt {Attempt}: {Message}", retry + 1, ex.Message);
                Console.WriteLine($"Error loading settings on attempt {retry + 1}: {ex.Message}");

                if (retry == maxRetries - 1) // Last attempt failed
                {
                    Snackbar.Add($"Error loading settings: {ex.Message}", Severity.Error);
                    Console.WriteLine($"Error loading settings: {ex.Message}");
                    globalSettings = new ApiSettingsModel();
                    stores = new List<StoreResponse>();
                }
            }
        }
    }

    private async Task LoadStoreSettings(int storeId)
    {
        try
        {
            Logger.LogInformation("Loading store settings for store {StoreId}", storeId);
            var response = await SyncApi.GetStoreByIdAsync(storeId);
            if (response != null)
            {
                storeSettings = response;
                Logger.LogInformation("Store settings loaded successfully for store {StoreId}", storeId);
            }
            else
            {
                Logger.LogWarning("Failed to load store settings for store {StoreId}", storeId);
                storeSettings = new StoreSettingsApiModel();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading store settings for store {StoreId}: {Message}", storeId, ex.Message);
            Snackbar.Add($"Error loading store settings: {ex.Message}", Severity.Error);
            storeSettings = new StoreSettingsApiModel();
        }
    }

    private async Task OnStoreChanged(int newStoreId)
    {
        selectedStoreId = newStoreId;
        await LoadStoreSettings(selectedStoreId);
        StateHasChanged();
    }

    private async Task SaveSettings()
    {
        Logger.LogInformation("=== SAVE SETTINGS BUTTON CLICKED ===");

        if (globalSettings == null)
        {
            Logger.LogWarning("SaveSettings called but globalSettings is null");
            Snackbar.Add("Global settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        if (storeSettings == null)
        {
            Logger.LogWarning("SaveSettings called but storeSettings is null");
            Snackbar.Add("Store settings not loaded. Please select a store.", Severity.Warning);
            return;
        }

        Logger.LogInformation("Starting to save settings");
        isSaving = true;
        StateHasChanged(); // Force UI update

        try
        {
            // Save global settings
            var globalSuccess = await SyncApi.UpdateSettingsAsync(globalSettings);
            Logger.LogInformation("UpdateSettingsAsync (global) returned: {Success}", globalSuccess);

            // Only save store settings if there are stores
            bool storeSuccess = true; // Default to true if no stores exist
            if (stores != null && stores.Count > 0)
            {
                // Save store settings
                storeSuccess = await SyncApi.UpdateStoreAsync(selectedStoreId, storeSettings);
                Logger.LogInformation("UpdateStoreAsync returned: {Success}", storeSuccess);

                if (globalSuccess && storeSuccess)
                {
                    Snackbar.Add("Settings saved successfully!", Severity.Success);
                    Logger.LogInformation("Settings saved successfully");
                }
                else if (!globalSuccess && !storeSuccess)
                {
                    Snackbar.Add("Failed to save both global and store settings", Severity.Error);
                    Logger.LogWarning("Both updates failed");
                }
                else if (!globalSuccess)
                {
                    Snackbar.Add("Global settings failed to save, but store settings saved successfully", Severity.Warning);
                    Logger.LogWarning("Global settings update failed");
                }
                else
                {
                    Snackbar.Add("Store settings failed to save, but global settings saved successfully", Severity.Warning);
                    Logger.LogWarning("Store settings update failed");
                }
            }
            else
            {
                // No stores, only global settings were saved
                if (globalSuccess)
                {
                    Snackbar.Add("Global settings saved successfully!", Severity.Success);
                    Logger.LogInformation("Global settings saved successfully (no stores configured)");
                }
                else
                {
                    Snackbar.Add("Failed to save global settings", Severity.Error);
                    Logger.LogWarning("Global settings update failed");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving settings: {Message}", ex.Message);
            Snackbar.Add($"Error saving settings: {ex.Message}", Severity.Error);
        }
        finally
        {
            isSaving = false;
            StateHasChanged(); // Force UI update
            Logger.LogInformation("=== SAVE SETTINGS COMPLETED ===");
        }
    }

    private async Task TestConnection(string service)
    {
        // Debug snackbar για να δούμε αν το κουμπί δουλεύει
        Snackbar.Add($"Button clicked for {service}!", Severity.Info);

        Logger.LogInformation("=== SETTINGS PAGE TEST CONNECTION START ===");
        Logger.LogInformation("User clicked Test Connection button for service: {Service}", service);

        if (globalSettings == null || storeSettings == null)
        {
            Logger.LogWarning("TestConnection called but settings are null");
            Snackbar.Add("Settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        isTestingConnection = true;
        testingService = service;
        StateHasChanged(); // Force UI update

        try
        {
            Logger.LogInformation("Calling SyncApi.TestConnectionAsync for service: {Service}", service);
            var result = await SyncApi.TestConnectionAsync(service);

            if (result == null)
            {
                Logger.LogWarning("TestConnectionAsync returned null for service: {Service}", service);
                Snackbar.Add($"No response from {service} test", Severity.Error);
                return;
            }

            Logger.LogInformation("TestConnectionAsync returned: Success={Success}, Message={Message} for service: {Service}",
                result.Success, result.Message, service);

            // Use the actual message from the API response
            var severity = result.Success ? Severity.Success : Severity.Error;

            Logger.LogInformation("Showing snackbar message: {Message} with severity: {Severity}", result.Message, severity);
            Snackbar.Add(result.Message, severity);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error during TestConnection for service: {Service}. Message: {Message}", service, ex.Message);
            var errorMessage = $"Connection error testing {service}: Unable to reach API service";
            Snackbar.Add(errorMessage, Severity.Error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during TestConnection for service: {Service}. Message: {Message}", service, ex.Message);
            var errorMessage = $"Error testing {service} connection: {ex.Message}";
            Logger.LogInformation("Showing error snackbar: {ErrorMessage}", errorMessage);
            Snackbar.Add(errorMessage, Severity.Error);
        }
        finally
        {
            Logger.LogInformation("TestConnection finally block - setting isTestingConnection to false");
            isTestingConnection = false;
            testingService = string.Empty;
            StateHasChanged(); // Force UI update
            Logger.LogInformation("=== SETTINGS PAGE TEST CONNECTION END ===");
        }
    }

    private async Task TestApiConnection()
    {
        try
        {
            Logger.LogInformation("Testing basic API connectivity...");
            Snackbar.Add("Testing API connection...", Severity.Info);

            // Simple test by calling the health endpoint
            var response = await SyncApi.GetSettingsAsync();
            if (response != null)
            {
                Logger.LogInformation("API test successful - settings retrieved");
                Snackbar.Add("API connection successful!", Severity.Success);
            }
            else
            {
                Logger.LogWarning("API test failed - no settings returned");
                Snackbar.Add("API connection failed - no data returned", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API test failed: {Message}", ex.Message);
            Snackbar.Add($"API connection error: {ex.Message}", Severity.Error);
        }
    }

    private async Task TestEmail()
    {
        Logger.LogInformation("=== TEST EMAIL START ===");
        Logger.LogInformation("User clicked Send Test Email button");

        if (globalSettings == null)
        {
            Logger.LogWarning("TestEmail called but globalSettings is null");
            Snackbar.Add("Settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(globalSettings.Email.SmtpHost) ||
            string.IsNullOrWhiteSpace(globalSettings.Email.FromEmail) ||
            string.IsNullOrWhiteSpace(globalSettings.Email.ToEmail))
        {
            Snackbar.Add("Please fill in all email settings before testing", Severity.Warning);
            return;
        }

        isTestingEmail = true;
        StateHasChanged(); // Force UI update

        try
        {
            Logger.LogInformation("Calling SyncApi.TestEmailAsync");
            var result = await SyncApi.TestEmailAsync();

            if (result == null)
            {
                Logger.LogWarning("TestEmailAsync returned null");
                Snackbar.Add("No response from email test", Severity.Error);
                return;
            }

            Logger.LogInformation("TestEmailAsync returned: Success={Success}, Message={Message}",
                result.Success, result.Message);

            var severity = result.Success ? Severity.Success : Severity.Error;
            Snackbar.Add(result.Message, severity);
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error during TestEmail: {Message}", ex.Message);
            Snackbar.Add("Connection error testing email: Unable to reach API service", Severity.Error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during TestEmail: {Message}", ex.Message);
            Snackbar.Add($"Error testing email: {ex.Message}", Severity.Error);
        }
        finally
        {
            Logger.LogInformation("TestEmail finally block - setting isTestingEmail to false");
            isTestingEmail = false;
            StateHasChanged(); // Force UI update
            Logger.LogInformation("=== TEST EMAIL END ===");
        }
    }

    private async Task ExportSettings()
    {
        Logger.LogInformation("=== EXPORT SETTINGS START ===");
        Logger.LogInformation("User clicked Export Settings button");

        if (globalSettings == null || storeSettings == null)
        {
            Logger.LogWarning("ExportSettings called but settings are null");
            Snackbar.Add("Settings not loaded. Please save settings first.", Severity.Warning);
            return;
        }

        isExporting = true;
        StateHasChanged();

        try
        {
            Logger.LogInformation("Opening export endpoint in new window");

            // Open the export endpoint in a new tab/window - this will trigger the download
            var exportUrl = "/api/settings/export";
            await JSRuntime.InvokeVoidAsync("open", exportUrl, "_blank");

            Snackbar.Add("Settings exported successfully! Check your downloads.", Severity.Success);
            Logger.LogInformation("Export successful");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting settings: {Message}", ex.Message);
            Snackbar.Add($"Error exporting settings: {ex.Message}", Severity.Error);
        }
        finally
        {
            isExporting = false;
            StateHasChanged();
            Logger.LogInformation("=== EXPORT SETTINGS END ===");
        }
    }
}