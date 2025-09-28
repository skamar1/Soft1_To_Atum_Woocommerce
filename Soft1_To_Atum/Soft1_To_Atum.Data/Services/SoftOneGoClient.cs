using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Soft1_To_Atum.Data.Services;

public class SoftOneGoClient : ISoftOneGoClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SoftOneGoClient> _logger;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    public SoftOneGoClient(HttpClient httpClient, IConfiguration configuration, ILogger<SoftOneGoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["SoftOneGo:BaseUrl"] ?? throw new ArgumentException("SoftOneGo:BaseUrl not configured");
        _apiKey = configuration["SoftOneGo:ApiKey"] ?? throw new ArgumentException("SoftOneGo:ApiKey not configured");

        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<List<SoftOneProduct>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching products from SoftOne Go API");

            var response = await _httpClient.GetAsync("/api/products", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var products = JsonSerializer.Deserialize<List<SoftOneProduct>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];

            _logger.LogInformation("Successfully fetched {count} products from SoftOne Go", products.Count);
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products from SoftOne Go API");
            throw;
        }
    }

    public async Task<SoftOneProduct?> GetProductByIdAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching product {productId} from SoftOne Go API", productId);

            var response = await _httpClient.GetAsync($"/api/products/{productId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var product = JsonSerializer.Deserialize<SoftOneProduct>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product {productId} from SoftOne Go API", productId);
            throw;
        }
    }
}