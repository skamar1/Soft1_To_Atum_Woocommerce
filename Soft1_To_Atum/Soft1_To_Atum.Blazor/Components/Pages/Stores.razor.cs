using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Data.Models;
using Soft1_To_Atum.Blazor.Services;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Stores : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<Stores> Logger { get; set; } = default!;

    private List<StoreSettingsApiModel> stores = new();
    private StoreSettingsApiModel? currentStore;
    private StoreSettingsApiModel? storeToDelete;

    private bool isLoading = false;
    private bool isSaving = false;
    private bool isDeleting = false;
    private bool isDialogOpen = false;
    private bool isDeleteDialogOpen = false;
    private bool isEditMode = false;

    private DialogOptions dialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseButton = true
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadStores();
    }

    private async Task LoadStores()
    {
        isLoading = true;
        try
        {
            var storeResponses = await SyncApi.GetStoresAsync();
            if (storeResponses != null)
            {
                // Convert StoreResponse to StoreSettingsApiModel by fetching details
                stores = new List<StoreSettingsApiModel>();
                foreach (var storeResp in storeResponses)
                {
                    var storeDetails = await SyncApi.GetStoreByIdAsync(storeResp.Id);
                    if (storeDetails != null)
                    {
                        stores.Add(storeDetails);
                    }
                }
            }
            else
            {
                stores = new List<StoreSettingsApiModel>();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading stores");
            Snackbar.Add($"Error loading stores: {ex.Message}", Severity.Error);
            stores = new List<StoreSettingsApiModel>();
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void OpenCreateDialog()
    {
        isEditMode = false;
        currentStore = new StoreSettingsApiModel
        {
            Id = 0,
            Name = "",
            Enabled = true,
            SoftOneGo = new SoftOneGoSettings
            {
                BaseUrl = "https://go.s1cloud.net/s1services",
                AppId = "703",
                Token = "",
                S1Code = "",
                Filters = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999"
            },
            ATUM = new AtumSettings
            {
                LocationId = 870,
                LocationName = "store_location"
            }
        };
        isDialogOpen = true;
    }

    private void OpenEditDialog(StoreSettingsApiModel store)
    {
        isEditMode = true;
        // Deep copy to avoid modifying the original
        currentStore = new StoreSettingsApiModel
        {
            Id = store.Id,
            Name = store.Name,
            Enabled = store.Enabled,
            SoftOneGo = new SoftOneGoSettings
            {
                BaseUrl = store.SoftOneGo.BaseUrl,
                AppId = store.SoftOneGo.AppId,
                Token = store.SoftOneGo.Token,
                S1Code = store.SoftOneGo.S1Code,
                Filters = store.SoftOneGo.Filters
            },
            ATUM = new AtumSettings
            {
                LocationId = store.ATUM.LocationId,
                LocationName = store.ATUM.LocationName
            }
        };
        isDialogOpen = true;
    }

    private void CloseDialog()
    {
        isDialogOpen = false;
        currentStore = null;
    }

    private async Task SaveStore()
    {
        if (currentStore == null || string.IsNullOrWhiteSpace(currentStore.Name))
        {
            Snackbar.Add("Please provide a store name", Severity.Warning);
            return;
        }

        isSaving = true;
        try
        {
            bool success;
            if (isEditMode)
            {
                success = await SyncApi.UpdateStoreAsync(currentStore.Id, currentStore);
                if (success)
                {
                    Snackbar.Add($"Store '{currentStore.Name}' updated successfully!", Severity.Success);
                }
            }
            else
            {
                // For create, we need to use POST endpoint
                var response = await SyncApi.CreateStoreAsync(currentStore);
                success = response != null;
                if (success)
                {
                    Snackbar.Add($"Store '{currentStore.Name}' created successfully!", Severity.Success);
                }
            }

            if (success)
            {
                CloseDialog();
                await LoadStores();
            }
            else
            {
                Snackbar.Add("Failed to save store", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving store");
            Snackbar.Add($"Error saving store: {ex.Message}", Severity.Error);
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private void OpenDeleteDialog(StoreSettingsApiModel store)
    {
        storeToDelete = store;
        isDeleteDialogOpen = true;
    }

    private void CloseDeleteDialog()
    {
        isDeleteDialogOpen = false;
        storeToDelete = null;
    }

    private async Task ConfirmDelete()
    {
        if (storeToDelete == null) return;

        isDeleting = true;
        try
        {
            var success = await SyncApi.DeleteStoreAsync(storeToDelete.Id);
            if (success)
            {
                Snackbar.Add($"Store '{storeToDelete.Name}' deleted successfully!", Severity.Success);
                CloseDeleteDialog();
                await LoadStores();
            }
            else
            {
                Snackbar.Add("Failed to delete store", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting store");
            Snackbar.Add($"Error deleting store: {ex.Message}", Severity.Error);
        }
        finally
        {
            isDeleting = false;
            StateHasChanged();
        }
    }
}
