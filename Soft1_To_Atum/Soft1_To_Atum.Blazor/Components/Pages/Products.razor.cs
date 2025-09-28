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

    private List<ProductResponse> products = new();
    private List<ProductResponse> filteredProducts = new();
    private bool loading = true;
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
        await LoadProducts();
    }

    private async Task LoadProducts()
    {
        try
        {
            loading = true;
            Logger.LogDebug("Loading products from API");

            // Call the products API endpoint
            var response = await SyncApi.GetProductsAsync();
            if (response != null)
            {
                products = response.Products;
                FilterProducts();
                Logger.LogDebug("Loaded {ProductCount} products", products.Count);
                Snackbar.Add($"Loaded {products.Count} products", Severity.Success);
            }
            else
            {
                Logger.LogWarning("No products returned from API");
                Snackbar.Add("No products found", Severity.Warning);
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error loading products: {Message}", ex.Message);
            Snackbar.Add("Failed to connect to API", Severity.Error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading products: {Message}", ex.Message);
            Snackbar.Add($"Error loading products: {ex.Message}", Severity.Error);
        }
        finally
        {
            loading = false;
            StateHasChanged();
        }
    }

    private async Task RefreshProducts()
    {
        Logger.LogInformation("User requested products refresh");
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
            // Note: This would ideally call a detailed API endpoint, but for now we'll create from available data
            selectedProduct = new Product
            {
                Id = productResponse.Id,
                SoftOneId = productResponse.SoftOneId,
                WooCommerceId = productResponse.WooCommerceId,
                AtumId = productResponse.AtumId,
                Name = productResponse.Name,
                Sku = productResponse.Sku,
                Price = productResponse.Price,
                Quantity = productResponse.Quantity,
                LastSyncedAt = productResponse.LastSyncedAt,
                LastSyncStatus = productResponse.LastSyncStatus,
                CreatedAt = DateTime.Now, // These would come from detailed API
                UpdatedAt = DateTime.Now,
                InternalId = "", // These would come from detailed API
                Barcode = "",
                Category = "",
                Unit = "",
                Group = "",
                Vat = "",
                WholesalePrice = null,
                SalePrice = null,
                PurchasePrice = null,
                Discount = null,
                ImageData = "",
                ZoomInfo = "",
                LastSyncError = null
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