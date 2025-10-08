using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Blazor.Services;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class GlobalSettings : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<GlobalSettings> Logger { get; set; } = default!;

    private ApiSettingsModel? settings;
    private bool isSaving = false;
    private bool isTestingConnection = false;
    private bool isTestingEmail = false;
    private string testingService = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadSettings();
            StateHasChanged();
        }
    }

    private async Task LoadSettings()
    {
        var maxRetries = 3;
        var delay = TimeSpan.FromSeconds(2);

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                Logger.LogDebug("Loading global settings (attempt {Attempt}/{MaxRetries})", retry + 1, maxRetries);

                settings = await SyncApi.GetSettingsAsync();
                if (settings == null)
                {
                    settings = new ApiSettingsModel();
                }

                Logger.LogDebug("Global settings loaded successfully");
                return;
            }
            catch (HttpRequestException ex) when (retry < maxRetries - 1)
            {
                Logger.LogWarning("Settings load attempt {Attempt} failed: {Message}. Retrying in {Delay}ms...",
                    retry + 1, ex.Message, delay.TotalMilliseconds);

                await Task.Delay(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 10));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading settings on attempt {Attempt}: {Message}", retry + 1, ex.Message);

                if (retry == maxRetries - 1)
                {
                    Snackbar.Add($"Error loading settings: {ex.Message}", Severity.Error);
                    settings = new ApiSettingsModel();
                }
            }
        }
    }

    private async Task SaveSettings()
    {
        if (settings == null)
        {
            Logger.LogWarning("SaveSettings called but settings is null");
            Snackbar.Add("Settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var success = await SyncApi.UpdateSettingsAsync(settings);
            Logger.LogInformation("UpdateSettingsAsync returned: {Success}", success);

            if (success)
            {
                Snackbar.Add("Global settings saved successfully!", Severity.Success);
                Logger.LogInformation("Global settings saved successfully");
            }
            else
            {
                Snackbar.Add("Failed to save global settings", Severity.Error);
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
            StateHasChanged();
        }
    }

    private async Task TestConnection(string service)
    {
        if (settings == null)
        {
            Snackbar.Add("Settings not loaded. Please refresh the page.", Severity.Warning);
            return;
        }

        isTestingConnection = true;
        testingService = service;
        StateHasChanged();

        try
        {
            var result = await SyncApi.TestConnectionAsync(service);

            if (result == null)
            {
                Snackbar.Add($"No response from {service} test", Severity.Error);
                return;
            }

            var severity = result.Success ? Severity.Success : Severity.Error;
            Snackbar.Add(result.Message, severity);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during TestConnection for service: {Service}", service);
            Snackbar.Add($"Error testing {service} connection: {ex.Message}", Severity.Error);
        }
        finally
        {
            isTestingConnection = false;
            testingService = string.Empty;
            StateHasChanged();
        }
    }

    private async Task TestEmail()
    {
        if (settings == null)
        {
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
        StateHasChanged();

        try
        {
            var result = await SyncApi.TestEmailAsync();

            if (result == null)
            {
                Snackbar.Add("No response from email test", Severity.Error);
                return;
            }

            var severity = result.Success ? Severity.Success : Severity.Error;
            Snackbar.Add(result.Message, severity);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during TestEmail");
            Snackbar.Add($"Error testing email: {ex.Message}", Severity.Error);
        }
        finally
        {
            isTestingEmail = false;
            StateHasChanged();
        }
    }
}
