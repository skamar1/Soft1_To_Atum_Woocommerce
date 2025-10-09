using Microsoft.AspNetCore.Components;
using MudBlazor;
using Soft1_To_Atum.Blazor.Services;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Blazor.Components.Pages;

public partial class Products : ComponentBase
{
    [Inject] private SyncApiClient SyncApi { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private ILogger<Products> Logger { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    private List<StoreResponse> stores = new();
    private int? selectedStoreId = null;
    private bool isLoadingStores = false;

    private List<ProductResponse> products = new();
    private List<ProductResponse> filteredProducts = new();
    private bool loading = false;
    private string searchTerm = "";
    private string _filterStatus = "All";
    private string filterStatus
    {
        get => _filterStatus;
        set
        {
            _filterStatus = value;
            FilterProducts();
            StateHasChanged();
        }
    }

    // Dialog state
    private bool showProductDialog = false;
    private Product? selectedProduct = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadStores();
        // Don't load products automatically - wait for user to select store and click Refresh
    }

    private async Task LoadStores()
    {
        isLoadingStores = true;
        try
        {
            var storeResponses = await SyncApi.GetStoresAsync();
            if (storeResponses != null)
            {
                stores = storeResponses.ToList();
                // Select first store by default
                if (stores.Count > 0)
                {
                    selectedStoreId = stores.First().Id;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading stores");
            Snackbar.Add($"Error loading stores: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoadingStores = false;
        }
    }

    private async Task LoadProducts()
    {
        try
        {
            loading = true;
            Logger.LogDebug("Loading products from API for store {StoreId}", selectedStoreId);

            // Call the GetAllProductsAsync method to get all products for the selected store
            var allProducts = await SyncApi.GetAllProductsAsync(selectedStoreId);
            if (allProducts != null)
            {
                products = allProducts;
                FilterProducts();
                Logger.LogDebug("Loaded {ProductCount} products for store {StoreId}", products.Count, selectedStoreId);
                Snackbar.Add($"Loaded {products.Count} products", Severity.Success);
            }
            else
            {
                Logger.LogWarning("No products returned from API for store {StoreId}", selectedStoreId);
                Snackbar.Add("No products found", Severity.Warning);
                products = new List<ProductResponse>();
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error loading products: {Message}", ex.Message);
            Snackbar.Add("Failed to connect to API", Severity.Error);
            products = new List<ProductResponse>();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading products: {Message}", ex.Message);
            Snackbar.Add($"Error loading products: {ex.Message}", Severity.Error);
            products = new List<ProductResponse>();
        }
        finally
        {
            loading = false;
            StateHasChanged();
        }
    }

    private async Task RefreshProducts()
    {
        if (selectedStoreId == null)
        {
            Snackbar.Add("Please select a store first", Severity.Warning);
            return;
        }

        Logger.LogInformation("User requested products refresh for store {StoreId}", selectedStoreId);
        await LoadProducts();
    }

    private void OnSearchKeyUp()
    {
        FilterProducts();
        StateHasChanged();
    }


    private void FilterProducts()
    {
        var filtered = products.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.ToLowerInvariant().Contains(searchLower) ||
                p.Sku.ToLowerInvariant().Contains(searchLower) ||
                p.SoftOneId.ToLowerInvariant().Contains(searchLower));
        }

        // Apply status filter
        if (filterStatus != "All")
        {
            filtered = filtered.Where(p => p.LastSyncStatus.Equals(filterStatus, StringComparison.OrdinalIgnoreCase));
        }

        filteredProducts = filtered.OrderByDescending(p => p.LastSyncedAt).ToList();
    }

    private void ViewProduct(ProductResponse productResponse)
    {
        try
        {
            Logger.LogDebug("Viewing product details for ID: {ProductId}", productResponse.Id);

            // Convert ProductResponse to Product for detailed view
            // Map all available data from ProductResponse to Product
            selectedProduct = new Product
            {
                Id = productResponse.Id,
                SoftOneId = productResponse.SoftOneId,
                WooCommerceId = productResponse.WooCommerceId,
                AtumId = productResponse.AtumId,
                InternalId = productResponse.InternalId,
                Sku = productResponse.Sku,
                Barcode = productResponse.Barcode,
                Name = productResponse.Name,
                Category = productResponse.Category,
                Unit = productResponse.Unit,
                Group = productResponse.Group,
                Vat = productResponse.Vat,
                Price = productResponse.Price,
                WholesalePrice = productResponse.WholesalePrice,
                SalePrice = productResponse.SalePrice,
                PurchasePrice = productResponse.PurchasePrice,
                Discount = productResponse.Discount,
                Quantity = productResponse.Quantity,
                AtumQuantity = productResponse.AtumQuantity,
                ImageData = productResponse.ImageData,
                ZoomInfo = productResponse.ZoomInfo,
                LastSyncedAt = productResponse.LastSyncedAt,
                CreatedAt = productResponse.CreatedAt,
                UpdatedAt = productResponse.UpdatedAt,
                LastSyncStatus = productResponse.LastSyncStatus,
                LastSyncError = productResponse.LastSyncError
            };

            showProductDialog = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error viewing product: {Message}", ex.Message);
            Snackbar.Add($"Error viewing product: {ex.Message}", Severity.Error);
        }
    }

    private void EditProduct(ProductResponse product)
    {
        // Placeholder for edit functionality
        Logger.LogInformation("Edit product requested for ID: {ProductId}", product.Id);
        Snackbar.Add("Edit functionality not yet implemented", Severity.Info);
    }

    private async Task DeleteAllProducts()
    {
        try
        {
            Logger.LogWarning("User requested to delete all products");

            // Show simple confirmation with snackbar
            var confirmed = await Task.FromResult(false); // Default to false for safety

            // Use a simple JavaScript confirm dialog for now
            var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.Small };

            var result = await DialogService.ShowMessageBox(
                "Confirm Delete",
                "⚠️ DANGER: This will permanently delete ALL products from the database. Are you sure?",
                yesText: "Delete All",
                cancelText: "Cancel");

            if (result == true)
            {
                Snackbar.Add("Deleting all products...", Severity.Warning);
                var success = await SyncApi.DeleteAllProductsAsync();

                if (success)
                {
                    Snackbar.Add("All products deleted successfully", Severity.Success);
                    // Refresh the products list
                    await LoadProducts();
                }
                else
                {
                    Snackbar.Add("Failed to delete products", Severity.Error);
                }
            }
            else
            {
                Snackbar.Add("Operation cancelled", Severity.Info);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting all products: {Message}", ex.Message);
            Snackbar.Add($"Error deleting products: {ex.Message}", Severity.Error);
        }
    }

    private static Color GetStatusColor(string status)
    {
        return status.ToLower() switch
        {
            "created" => Color.Success,
            "updated" => Color.Info,
            "error" or "failed" => Color.Error,
            "skipped" => Color.Warning,
            _ => Color.Default
        };
    }
}