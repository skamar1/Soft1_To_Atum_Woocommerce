using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Soft1_To_Atum.Data.Services;

public class WooCommerceAtumClient : IWooCommerceAtumClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WooCommerceAtumClient> _logger;
    private readonly SyncDbContext _dbContext;

    public WooCommerceAtumClient(HttpClient httpClient, ILogger<WooCommerceAtumClient> logger, SyncDbContext dbContext)
    {
        _httpClient = httpClient;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<List<WooCommerceProduct>> GetProductsAsync(int storeId, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(storeId, cancellationToken);
        var client = CreateAuthenticatedClient(store);

        try
        {
            _logger.LogInformation("Fetching products from WooCommerce store {storeId}", storeId);

            var response = await client.GetAsync("/wp-json/wc/v3/products?per_page=100", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var products = JsonSerializer.Deserialize<List<WooCommerceProduct>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            _logger.LogInformation("Successfully fetched {count} products from store {storeId}", products.Count, storeId);
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products from WooCommerce store {storeId}", storeId);
            throw;
        }
    }

    public async Task<WooCommerceProduct?> GetProductBySkuAsync(int storeId, string sku, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(storeId, cancellationToken);
        var client = CreateAuthenticatedClient(store);

        try
        {
            _logger.LogDebug("Fetching product with SKU {sku} from store {storeId}", sku, storeId);

            var response = await client.GetAsync($"/wp-json/wc/v3/products?sku={Uri.EscapeDataString(sku)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var products = JsonSerializer.Deserialize<List<WooCommerceProduct>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            return products.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product with SKU {sku} from store {storeId}", sku, storeId);
            throw;
        }
    }

    public async Task<WooCommerceProduct> CreateProductAsync(int storeId, CreateWooCommerceProductRequest request, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(storeId, cancellationToken);
        var client = CreateAuthenticatedClient(store);

        try
        {
            _logger.LogInformation("Creating product {sku} in store {storeId}", request.Sku, storeId);

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/wp-json/wc/v3/products", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var product = JsonSerializer.Deserialize<WooCommerceProduct>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize created product");

            _logger.LogInformation("Successfully created product {sku} with ID {productId} in store {storeId}",
                request.Sku, product.Id, storeId);

            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product {sku} in store {storeId}", request.Sku, storeId);
            throw;
        }
    }

    public async Task<WooCommerceProduct> UpdateProductAsync(int storeId, int productId, UpdateWooCommerceProductRequest request, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(storeId, cancellationToken);
        var client = CreateAuthenticatedClient(store);

        try
        {
            _logger.LogInformation("Updating product {productId} in store {storeId}", productId, storeId);

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/wp-json/wc/v3/products/{productId}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var product = JsonSerializer.Deserialize<WooCommerceProduct>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize updated product");

            _logger.LogInformation("Successfully updated product {productId} in store {storeId}", productId, storeId);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {productId} in store {storeId}", productId, storeId);
            throw;
        }
    }

    public async Task UpdateAtumInventoryAsync(int storeId, int productId, UpdateAtumInventoryRequest request, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(storeId, cancellationToken);
        var client = CreateAuthenticatedClient(store);

        try
        {
            _logger.LogInformation("Updating ATUM inventory for product {productId} in store {storeId}", productId, storeId);

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"/wp-json/atum/v1/inventory/{productId}", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully updated ATUM inventory for product {productId} in store {storeId}", productId, storeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ATUM inventory for product {productId} in store {storeId}", productId, storeId);
            throw;
        }
    }

    private async Task<Models.Store> GetStoreAsync(int storeId, CancellationToken cancellationToken)
    {
        var store = await _dbContext.Stores
            .FirstOrDefaultAsync(s => s.Id == storeId && s.IsActive, cancellationToken);

        return store ?? throw new ArgumentException($"Store with ID {storeId} not found or inactive");
    }

    private HttpClient CreateAuthenticatedClient(Models.Store store)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(store.WooCommerceUrl)
        };

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{store.WooCommerceKey}:{store.WooCommerceSecret}"));
        client.DefaultRequestHeaders.Add("Authorization", $"Basic {credentials}");

        return client;
    }
}