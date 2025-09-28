using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;
using System.Net.Http;
using System.Text.Json;

namespace Soft1_To_Atum.Data.Services;

public class AtumApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AtumApiService> _logger;

    public AtumApiService(HttpClient httpClient, ILogger<AtumApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetch all ATUM inventory items for a specific location with pagination
    /// </summary>
    public async Task<List<AtumInventoryItem>> GetAllInventoryAsync(
        string consumerKey,
        string consumerSecret,
        int locationId,
        CancellationToken cancellationToken = default)
    {
        var allItems = new List<AtumInventoryItem>();
        int page = 1;
        const int perPage = 100;
        bool hasMoreData = true;

        _logger.LogInformation("Starting ATUM inventory fetch for location {LocationId}", locationId);

        while (hasMoreData && page <= 100) // Safety limit to prevent infinite loops
        {
            try
            {
                var pageItems = await GetInventoryPageAsync(consumerKey, consumerSecret, locationId, page, perPage, cancellationToken);

                if (pageItems.Any())
                {
                    allItems.AddRange(pageItems);
                    _logger.LogDebug("Fetched page {Page} with {Count} items", page, pageItems.Count);

                    // If we got less than perPage items, we've reached the end
                    hasMoreData = pageItems.Count == perPage;
                    page++;
                }
                else
                {
                    hasMoreData = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ATUM inventory page {Page}", page);
                throw;
            }
        }

        _logger.LogInformation("Completed ATUM inventory fetch. Total items: {TotalCount}", allItems.Count);
        return allItems;
    }

    /// <summary>
    /// Fetch a single page of ATUM inventory items
    /// </summary>
    private async Task<List<AtumInventoryItem>> GetInventoryPageAsync(
        string consumerKey,
        string consumerSecret,
        int locationId,
        int page,
        int perPage,
        CancellationToken cancellationToken)
    {
        var url = $"https://panes.gr/wp-json/wc/v3/atum/inventories" +
                  $"?consumer_key={consumerKey}" +
                  $"&consumer_secret={consumerSecret}" +
                  $"&location={locationId}" +
                  $"&per_page={perPage}" +
                  $"&page={page}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", "shop_per_page=100");

        _logger.LogDebug("Fetching ATUM inventory page {Page} from {Url}", page, url);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogTrace("ATUM API Response: {Response}", jsonContent);

        // ATUM API returns array directly, not wrapped in response object
        var items = JsonSerializer.Deserialize<List<AtumInventoryItem>>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        return items;
    }
}