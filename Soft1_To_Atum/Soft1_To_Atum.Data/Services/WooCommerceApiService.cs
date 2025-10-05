using Microsoft.Extensions.Logging;
using Models = Soft1_To_Atum.Data.Models;
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
    public async Task<List<Models.WooCommerceProduct>> GetAllProductsAsync(
        string consumerKey,
        string consumerSecret,
        CancellationToken cancellationToken = default)
    {
        var allProducts = new List<Models.WooCommerceProduct>();
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
                    _logger.LogDebug("\n\nFetched page {Page} with {Count} products\n\n", page, pageProducts.Count);

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
    /// Fetch all WooCommerce products with parallel processing for better performance
    /// </summary>
    public async Task<List<Models.WooCommerceProduct>> GetAllProductsParallelAsync(
        string consumerKey,
        string consumerSecret,
        int maxConcurrency = 3,
        CancellationToken cancellationToken = default)
    {
        var allProducts = new List<Models.WooCommerceProduct>();
        const int perPage = 100;
        const int maxPages = 100; // Safety limit

        _logger.LogInformation("Starting parallel WooCommerce products fetch with max concurrency: {MaxConcurrency}", maxConcurrency);

        // First, get the first page to determine if there are products
        var firstPageProducts = await GetProductsPageAsync(consumerKey, consumerSecret, 1, perPage, cancellationToken);

        if (!firstPageProducts.Any())
        {
            _logger.LogInformation("No products found in WooCommerce");
            return allProducts;
        }

        allProducts.AddRange(firstPageProducts);

        // If we got a full page, there might be more pages
        if (firstPageProducts.Count == perPage)
        {
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new List<Task<List<Models.WooCommerceProduct>>>();

            // Start from page 2 since we already have page 1
            for (int page = 2; page <= maxPages; page++)
            {
                var currentPage = page;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var pageProducts = await GetProductsPageAsync(consumerKey, consumerSecret, currentPage, perPage, cancellationToken);
                        _logger.LogDebug("Fetched page {Page} with {Count} products", currentPage, pageProducts.Count);
                        return pageProducts;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch page {Page}, returning empty list", currentPage);
                        return new List<Models.WooCommerceProduct>();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Process results and stop when we encounter an empty page
            bool foundEmptyPage = false;
            foreach (var pageProducts in results)
            {
                if (foundEmptyPage || pageProducts.Count == 0)
                {
                    foundEmptyPage = true;
                    continue; // Skip empty pages after we found the first empty one
                }

                allProducts.AddRange(pageProducts);

                // If this page has fewer products than perPage, it's the last page with data
                if (pageProducts.Count < perPage)
                {
                    foundEmptyPage = true;
                }
            }
        }

        _logger.LogInformation("Completed parallel WooCommerce products fetch. Total products: {TotalCount}", allProducts.Count);
        return allProducts;
    }

    /// <summary>
    /// Fetch a single page of WooCommerce products (made public for background service)
    /// </summary>
    public async Task<List<Models.WooCommerceProduct>> GetProductsPageAsync(
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
                  $"&page={page}" +
                  $"&orderby=id&order=asc"; // Ensure deterministic ordering by WooCommerce ID

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

        var products = JsonSerializer.Deserialize<List<Models.WooCommerceProduct>>(jsonContent, options) ?? [];

        return products;
    }

    /// <summary>
    /// Create a new product in WooCommerce
    /// </summary>
    public async Task<Models.WooCommerceProduct?> CreateProductAsync(
        string consumerKey,
        string consumerSecret,
        string name,
        string sku,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://panes.gr/wp-json/wc/v3/products" +
                  $"?consumer_key={consumerKey}" +
                  $"&consumer_secret={consumerSecret}";

        var productData = new
        {
            name = name,
            sku = sku,
            regular_price = price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            status = "draft",  // Draft status - needs manual review before publishing
            manage_stock = true,
            stock_quantity = 0
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(productData);
        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        _logger.LogDebug("Creating WooCommerce product: {Name} (SKU: {Sku})", name, sku);

        try
        {
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("WooCommerce API error creating product {Name}. Status: {StatusCode}, Response: {Response}",
                    name, response.StatusCode, responseContent);
                response.EnsureSuccessStatusCode(); // This will throw with the status code
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            options.Converters.Add(new SafeDecimalConverter());

            var product = JsonSerializer.Deserialize<Models.WooCommerceProduct>(responseContent, options);

            _logger.LogInformation("Created WooCommerce product {Name} with ID {ProductId}", name, product?.Id);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating WooCommerce product {Name} (SKU: {Sku})", name, sku);
            throw;
        }
    }

    /// <summary>
    /// Fetch specific products by their IDs with include parameter
    /// </summary>
    public async Task<List<Models.WooCommerceProduct>> GetProductsByIdsAsync(
        string consumerKey,
        string consumerSecret,
        List<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (!productIds.Any())
            return [];

        var allProducts = new List<Models.WooCommerceProduct>();
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

                var batchProducts = JsonSerializer.Deserialize<List<Models.WooCommerceProduct>>(jsonContent, options) ?? [];
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

    /// <summary>
    /// Search for a product by SKU in WooCommerce
    /// </summary>
    public async Task<Models.WooCommerceProduct?> GetProductBySkuAsync(
        string consumerKey,
        string consumerSecret,
        string sku,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sku))
            return null;

        var url = $"https://panes.gr/wp-json/wc/v3/products" +
                  $"?consumer_key={consumerKey}" +
                  $"&consumer_secret={consumerSecret}" +
                  $"&sku={sku}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", "shop_per_page=100");

        _logger.LogDebug("Searching WooCommerce product by SKU: {Sku}", sku);

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

            // WooCommerce returns an array even for SKU search
            var products = JsonSerializer.Deserialize<List<Models.WooCommerceProduct>>(jsonContent, options);

            if (products?.Any() == true)
            {
                _logger.LogInformation("Found WooCommerce product with SKU {Sku}: ID {ProductId}", sku, products[0].Id);
                return products[0]; // Return first match
            }

            _logger.LogDebug("No WooCommerce product found with SKU {Sku}", sku);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching WooCommerce product by SKU: {Sku}", sku);
            throw;
        }
    }
}