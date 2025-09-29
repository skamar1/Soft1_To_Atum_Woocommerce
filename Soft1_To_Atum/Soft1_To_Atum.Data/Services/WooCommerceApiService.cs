using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;
using System.Net.Http;
using System.Text.Json;
using Soft1_To_Atum.Data.Json;

namespace Soft1_To_Atum.Data.Services;

public class WooCommerceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WooCommerceApiService> _logger;

    public WooCommerceApiService(HttpClient httpClient, ILogger<WooCommerceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetch all WooCommerce products with pagination
    /// </summary>
    public async Task<List<WooCommerceProduct>> GetAllProductsAsync(
        string consumerKey,
        string consumerSecret,
        CancellationToken cancellationToken = default)
    {
        var allProducts = new List<WooCommerceProduct>();
        int page = 1;
        const int perPage = 100;
        bool hasMoreData = true;

        _logger.LogInformation("Starting WooCommerce products fetch");

        while (hasMoreData && page <= 100) // Safety limit to prevent infinite loops
        {
            try
            {
                var pageProducts = await GetProductsPageAsync(consumerKey, consumerSecret, page, perPage, cancellationToken);

                if (pageProducts.Any())
                {
                    allProducts.AddRange(pageProducts);
                    _logger.LogDebug("Fetched page {Page} with {Count} products", page, pageProducts.Count);

                    // If we got less than perPage products, we've reached the end
                    hasMoreData = pageProducts.Count == perPage;
                    page++;
                }
                else
                {
                    hasMoreData = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching WooCommerce products page {Page}", page);
                throw;
            }
        }

        _logger.LogInformation("Completed WooCommerce products fetch. Total products: {TotalCount}", allProducts.Count);
        return allProducts;
    }

    /// <summary>
    /// Fetch a single page of WooCommerce products
    /// </summary>
    private async Task<List<WooCommerceProduct>> GetProductsPageAsync(
        string consumerKey,
        string consumerSecret,
        int page,
        int perPage,
        CancellationToken cancellationToken)
    {
        var url = $"https://panes.gr/wp-json/wc/v3/products" +
                  $"?consumer_key={consumerKey}" +
                  $"&consumer_secret={consumerSecret}" +
                  $"&per_page={perPage}" +
                  $"&page={page}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", "shop_per_page=100");

        _logger.LogDebug("Fetching WooCommerce products page {Page} from {Url}", page, url);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogTrace("WooCommerce API Response: {Response}", jsonContent);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };
        options.Converters.Add(new SafeDecimalConverter());

        var products = JsonSerializer.Deserialize<List<WooCommerceProduct>>(jsonContent, options) ?? [];

        return products;
    }

    /// <summary>
    /// Fetch specific products by their IDs with include parameter
    /// </summary>
    public async Task<List<WooCommerceProduct>> GetProductsByIdsAsync(
        string consumerKey,
        string consumerSecret,
        List<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (!productIds.Any())
            return [];

        var allProducts = new List<WooCommerceProduct>();
        const int batchSize = 100; // WooCommerce limit for include parameter

        // Process products in batches
        for (int i = 0; i < productIds.Count; i += batchSize)
        {
            var batch = productIds.Skip(i).Take(batchSize).ToList();
            var includeIds = string.Join(",", batch);

            var url = $"https://panes.gr/wp-json/wc/v3/products" +
                      $"?consumer_key={consumerKey}" +
                      $"&consumer_secret={consumerSecret}" +
                      $"&include={includeIds}" +
                      $"&per_page={batchSize}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", "shop_per_page=100");

            _logger.LogDebug("Fetching WooCommerce products by IDs: {ProductIds}", includeIds);

            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };
                options.Converters.Add(new SafeDecimalConverter());

                var batchProducts = JsonSerializer.Deserialize<List<WooCommerceProduct>>(jsonContent, options) ?? [];
                allProducts.AddRange(batchProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching WooCommerce products by IDs: {ProductIds}", includeIds);
                throw;
            }
        }

        _logger.LogInformation("Fetched {Count} WooCommerce products by IDs", allProducts.Count);
        return allProducts;
    }
}