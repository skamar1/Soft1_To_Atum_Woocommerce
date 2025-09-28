using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Blazor.Services;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Home : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private ILogger<Home> Logger { get; set; } = null!;

    private bool isSyncRunning = false;
    private bool isAtumSyncRunning = false;
    private string lastSyncMessage = string.Empty;
    private Severity lastSyncSeverity = Severity.Info;
    private SyncLogResponse? lastSyncLog;

    protected override async Task OnInitializedAsync()
    {
        await RefreshDashboard();
    }

    private async Task StartManualSync()
    {
        if (isSyncRunning) return;

        Logger.LogInformation("User initiated manual sync from dashboard");
        isSyncRunning = true;
        lastSyncMessage = "Starting synchronization...";
        lastSyncSeverity = Severity.Info;
        StateHasChanged();

        try
        {
            var result = await SyncApi.StartManualSyncAsync();

            if (result != null)
            {
                lastSyncMessage = result.Message;
                lastSyncSeverity = Severity.Success;
                Logger.LogInformation("Manual sync completed successfully. SyncLogId: {SyncLogId}", result.SyncLogId);

                // Refresh dashboard to show updated statistics
                await RefreshDashboard();

                Snackbar.Add("Sync completed successfully!", Severity.Success);
            }
            else
            {
                lastSyncMessage = "Sync completed but no details were returned";
                lastSyncSeverity = Severity.Warning;
                Logger.LogWarning("Manual sync returned null result");

                Snackbar.Add("Sync completed but response was empty", Severity.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            lastSyncMessage = $"Connection error: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "HTTP error during manual sync: {Message}", ex.Message);

            Snackbar.Add("Failed to connect to sync service", Severity.Error);
        }
        catch (Exception ex)
        {
            lastSyncMessage = $"Sync failed: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "Error during manual sync: {Message}", ex.Message);

            Snackbar.Add($"Sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isSyncRunning = false;
            StateHasChanged();
        }
    }

    private async Task StartAtumSync()
    {
        if (isAtumSyncRunning) return;

        Logger.LogInformation("User initiated ATUM sync from dashboard");
        isAtumSyncRunning = true;
        lastSyncMessage = "Starting ATUM synchronization...";
        lastSyncSeverity = Severity.Info;
        StateHasChanged();

        try
        {
            var result = await SyncApi.StartAtumSyncAsync();

            if (result != null)
            {
                lastSyncMessage = result.Message;
                lastSyncSeverity = Severity.Success;
                Logger.LogInformation("ATUM sync completed successfully. SyncLogId: {SyncLogId}", result.SyncLogId);

                // Refresh dashboard to show updated statistics
                await RefreshDashboard();

                Snackbar.Add("ATUM sync completed successfully!", Severity.Success);
            }
            else
            {
                lastSyncMessage = "ATUM sync completed but no details were returned";
                lastSyncSeverity = Severity.Warning;
                Logger.LogWarning("ATUM sync returned null result");

                Snackbar.Add("ATUM sync completed but response was empty", Severity.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            lastSyncMessage = $"Connection error: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "HTTP error during ATUM sync: {Message}", ex.Message);

            Snackbar.Add("Failed to connect to sync service", Severity.Error);
        }
        catch (Exception ex)
        {
            lastSyncMessage = $"ATUM sync failed: {ex.Message}";
            lastSyncSeverity = Severity.Error;
            Logger.LogError(ex, "Error during ATUM sync: {Message}", ex.Message);

            Snackbar.Add($"ATUM sync failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isAtumSyncRunning = false;
            StateHasChanged();
        }
    }

    private async Task RefreshDashboard()
    {
        try
        {
            Logger.LogDebug("Refreshing dashboard data");

            // Load the most recent sync log
            var syncLogs = await SyncApi.GetSyncLogsAsync(); // Get all sync logs

            if (syncLogs?.Any() == true)
            {
                lastSyncLog = syncLogs.OrderByDescending(s => s.StartedAt).First();
                Logger.LogDebug("Loaded last sync log: {SyncLogId} - {Status}", lastSyncLog.Id, lastSyncLog.Status);
            }
            else
            {
                lastSyncLog = null;
                Logger.LogDebug("No sync logs found");
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error refreshing dashboard: {Message}", ex.Message);
            Snackbar.Add("Failed to refresh dashboard data", Severity.Warning);
        }
    }

    private async Task LoadSyncHistory()
    {
        try
        {
            // For now, just refresh the dashboard
            // Later we can implement a dedicated sync history page/dialog
            await RefreshDashboard();
            Snackbar.Add("Dashboard refreshed with latest sync data", Severity.Info);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading sync history: {Message}", ex.Message);
            Snackbar.Add("Failed to load sync history", Severity.Error);
        }
    }

    private async Task TestConnections()
    {
        try
        {
            Snackbar.Add("Testing connections...", Severity.Info);

            // Test SoftOne connection
            var softOneResult = await SyncApi.TestConnectionAsync("softone");
            if (softOneResult.Success)
            {
                Snackbar.Add("SoftOne connection successful", Severity.Success);
            }
            else
            {
                Snackbar.Add($"SoftOne connection failed: {softOneResult.Message}", Severity.Error);
            }

            // Test WooCommerce connection
            var wooResult = await SyncApi.TestConnectionAsync("woocommerce");
            if (wooResult.Success)
            {
                Snackbar.Add("WooCommerce connection successful", Severity.Success);
            }
            else
            {
                Snackbar.Add($"WooCommerce connection failed: {wooResult.Message}", Severity.Error);
            }

            // Test email
            var emailResult = await SyncApi.TestEmailAsync();
            if (emailResult.Success)
            {
                Snackbar.Add("Email test successful", Severity.Success);
            }
            else
            {
                Snackbar.Add($"Email test failed: {emailResult.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error testing connections: {Message}", ex.Message);
            Snackbar.Add("Failed to test connections", Severity.Error);
        }
    }

    private static Color GetStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "completed" => Color.Success,
            "running" => Color.Info,
            "failed" => Color.Error,
            _ => Color.Default
        };
    }

    private static string GetDurationString(SyncLogResponse syncLog)
    {
        if (syncLog.CompletedAt.HasValue)
        {
            var duration = syncLog.CompletedAt.Value - syncLog.StartedAt;
            return duration.ToString(@"hh\:mm\:ss");
        }
        return "-";
    }
}