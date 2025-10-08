using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Blazor.Services;
using Soft1_To_Atum.Data.Models;
using System.Text.Json;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Logs : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private ILogger<Logs> Logger { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    private List<AutoSyncLog> autoSyncLogs = new();
    private List<SyncLogResponse> manualSyncLogs = new();
    private bool isLoadingAutoSync = false;
    private bool isLoadingManual = false;

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadAutoSyncLogs(), LoadManualSyncLogs());
    }

    private async Task LoadAutoSyncLogs()
    {
        isLoadingAutoSync = true;
        try
        {
            var logs = await SyncApi.GetAutoSyncLogsAsync();
            if (logs != null)
            {
                autoSyncLogs = logs.OrderByDescending(l => l.StartedAt).ToList();
                Logger.LogInformation("Loaded {Count} auto-sync logs", autoSyncLogs.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading auto-sync logs");
            Snackbar.Add("Σφάλμα κατά τη φόρτωση των auto-sync logs", Severity.Error);
        }
        finally
        {
            isLoadingAutoSync = false;
            StateHasChanged();
        }
    }

    private async Task LoadManualSyncLogs()
    {
        isLoadingManual = true;
        try
        {
            var logs = await SyncApi.GetSyncLogsAsync();
            if (logs != null)
            {
                manualSyncLogs = logs.OrderByDescending(l => l.StartedAt).ToList();
                Logger.LogInformation("Loaded {Count} manual sync logs", manualSyncLogs.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading manual sync logs");
            Snackbar.Add("Σφάλμα κατά τη φόρτωση των manual sync logs", Severity.Error);
        }
        finally
        {
            isLoadingManual = false;
            StateHasChanged();
        }
    }

    private static Color GetAutoSyncStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "completed" => Color.Success,
            "running" => Color.Info,
            "failed" => Color.Error,
            "cancelled" => Color.Warning,
            _ => Color.Default
        };
    }

    private static string GetAutoSyncStatusText(string status)
    {
        return status.ToLower() switch
        {
            "completed" => "Ολοκληρώθηκε",
            "running" => "Εκτελείται",
            "failed" => "Αποτυχία",
            "cancelled" => "Ακυρώθηκε",
            _ => status
        };
    }

    private static Color GetManualSyncStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "completed" => Color.Success,
            "running" => Color.Info,
            "failed" => Color.Error,
            _ => Color.Default
        };
    }

    private static string GetAutoSyncDuration(AutoSyncLog log)
    {
        if (log.CompletedAt.HasValue)
        {
            var duration = log.CompletedAt.Value - log.StartedAt;
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}ώ {duration.Minutes}λ {duration.Seconds}δ";
            else if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}λ {duration.Seconds}δ";
            else
                return $"{duration.Seconds}δ";
        }
        return "Εκτελείται...";
    }

    private static string GetManualSyncDuration(SyncLogResponse log)
    {
        if (log.CompletedAt.HasValue)
        {
            var duration = log.CompletedAt.Value - log.StartedAt;
            if (duration.TotalHours >= 1)
                return $"{(int)duration.TotalHours}ώ {duration.Minutes}λ {duration.Seconds}δ";
            else if (duration.TotalMinutes >= 1)
                return $"{(int)duration.TotalMinutes}λ {duration.Seconds}δ";
            else
                return $"{duration.Seconds}δ";
        }
        return "Εκτελείται...";
    }

    private async Task ShowAutoSyncDetails(AutoSyncLog log)
    {
        if (string.IsNullOrEmpty(log.Details))
        {
            Snackbar.Add("Δεν υπάρχουν λεπτομέρειες για αυτό το log", Severity.Info);
            return;
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(log.Details);
            if (dict == null)
            {
                Snackbar.Add("Σφάλμα κατά την ανάλυση των λεπτομερειών", Severity.Error);
                return;
            }

            var storeDetails = new List<StoreDetailViewModel>();
            foreach (var kvp in dict)
            {
                var detail = kvp.Value;
                storeDetails.Add(new StoreDetailViewModel
                {
                    StoreName = detail.TryGetProperty("storeName", out var storeName) ? storeName.GetString() ?? "" : "",
                    StoreId = detail.TryGetProperty("storeId", out var storeId) ? storeId.GetInt32() : 0,
                    SoftOneSync = detail.TryGetProperty("softOneSync", out var softOne) ? softOne.GetString() ?? "" : "",
                    AtumSync = detail.TryGetProperty("atumSync", out var atum) ? atum.GetString() ?? "" : "",
                    FullSync = detail.TryGetProperty("fullSync", out var full) ? full.GetString() ?? "" : "",
                    Status = detail.TryGetProperty("status", out var status) ? status.GetString() ?? "" : "",
                    Error = detail.TryGetProperty("error", out var error) ? error.GetString() : null
                });
            }

            var parameters = new DialogParameters
            {
                ["StoreDetails"] = storeDetails,
                ["LogStartedAt"] = log.StartedAt,
                ["LogCompletedAt"] = log.CompletedAt
            };

            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Large,
                FullWidth = true,
                CloseButton = true
            };

            await DialogService.ShowAsync<AutoSyncDetailsDialog>("Λεπτομέρειες Αυτόματου Συγχρονισμού", parameters, options);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing auto-sync details");
            Snackbar.Add("Σφάλμα κατά την εμφάνιση των λεπτομερειών", Severity.Error);
        }
    }

    private async Task ShowErrorMessage(string errorMessage)
    {
        var parameters = new DialogParameters
        {
            ["ContentText"] = errorMessage,
            ["ButtonText"] = "OK",
            ["Color"] = Color.Error
        };

        await DialogService.ShowMessageBox("Σφάλμα Συγχρονισμού", errorMessage, "OK");
    }

    private class StoreDetailViewModel
    {
        public string StoreName { get; set; } = "";
        public int StoreId { get; set; }
        public string SoftOneSync { get; set; } = "";
        public string AtumSync { get; set; } = "";
        public string FullSync { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Error { get; set; }
    }
}
