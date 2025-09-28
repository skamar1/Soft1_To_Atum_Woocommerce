using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Blazor.Services;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Settings : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<Settings> Logger { get; set; } = default!;

    private ApiSettingsModel? settings;
    private bool isSaving = false;
    private bool isTestingConnection = false;
    private bool isTestingEmail = false;
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

                settings = await SyncApi.GetSettingsAsync();
                if (settings == null)
                {
                    settings = new ApiSettingsModel();
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
                    settings = new ApiSettingsModel();
                }
            }
        }
    }

    private async Task SaveSettings()
    {
        Logger.LogInformation("=== SAVE SETTINGS BUTTON CLICKED ===");

        if (settings == null)
        {
            Logger.LogWarning("SaveSettings called but settings is null");
            Snackbar.Add("Settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        Logger.LogInformation("Starting to save settings for store: {StoreName}", settings.Name);
        isSaving = true;
        StateHasChanged(); // Force UI update

        try
        {
            var success = await SyncApi.UpdateSettingsAsync(settings);
            Logger.LogInformation("UpdateSettingsAsync returned: {Success}", success);

            if (success)
            {
                Snackbar.Add("Settings saved successfully!", Severity.Success);
                Logger.LogInformation("Settings saved successfully");
            }
            else
            {
                Snackbar.Add("Failed to save settings", Severity.Error);
                Logger.LogWarning("UpdateSettingsAsync returned false");
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

        if (settings == null)
        {
            Logger.LogWarning("TestConnection called but settings is null");
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

        if (settings == null)
        {
            Logger.LogWarning("TestEmail called but settings is null");
            Snackbar.Add("Settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Email.SmtpHost) ||
            string.IsNullOrWhiteSpace(settings.Email.FromEmail) ||
            string.IsNullOrWhiteSpace(settings.Email.ToEmail))
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
}