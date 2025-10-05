using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using static Soft1_To_Atum.Data.Models.AtumModels;

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
    // private async Task<List<AtumInventoryItem>> GetInventoryPageAsync(
    //     string consumerKey,
    //     string consumerSecret,
    //     int locationId,
    //     int page,
    //     int perPage,
    //     CancellationToken cancellationToken)
    // {
    //     var url = $"https://panes.gr/wp-json/wc/v3/atum/inventories" +
    //               $"?consumer_key={consumerKey}" +
    //               $"&consumer_secret={consumerSecret}" +
    //               $"&location={locationId}" +
    //               $"&per_page={perPage}" +
    //               $"&page={page}";

    //     var request = new HttpRequestMessage(HttpMethod.Get, url);
    //     request.Headers.Add("Cookie", "shop_per_page=100");

    //     _logger.LogDebug("Fetching ATUM inventory page {Page} from {Url}", page, url);

    //     var response = await _httpClient.SendAsync(request, cancellationToken);
    //     response.EnsureSuccessStatusCode();

    //     var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);

    //     _logger.LogTrace("ATUM API Response: {Response}", jsonContent);

    //     // ATUM API returns array directly, not wrapped in response object
    //     var items = JsonSerializer.Deserialize<List<AtumInventoryItem>>(jsonContent, new JsonSerializerOptions
    //     {
    //         PropertyNameCaseInsensitive = true
    //     }) ?? [];

    //     return items;
    // }

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

        // Deserialize
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        };

        var inventories = JsonSerializer.Deserialize<List<Inventory>>(jsonContent, options);


        List<AtumInventoryItem> items = new List<AtumInventoryItem>();

        for (int i = 0; inventories.Count > i; i++)
        {
            AtumMetaData MetaData = new()
            {
                Sku = inventories[i].MetaData.Sku,
                ManageStock = inventories[i].MetaData.ManageStock,
                StockQuantity = inventories[i].MetaData.StockQuantity,
                Barcode = inventories[i].MetaData.Barcode,
            };

            AtumInventoryItem item = new AtumInventoryItem
            {
                Id = inventories[i].Id,
                ProductId = inventories[i].ProductId,
                Name = inventories[i].Name,
                Priority = inventories[i].Priority,
                IsMain = inventories[i].IsMain,
                InventoryDate = inventories[i].InventoryDate,    
                MetaData = MetaData
            };

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Perform batch operations on ATUM inventories (create, update, delete)
    /// </summary>
    public async Task<AtumBatchResponse> BatchUpdateInventoryAsync(
        string consumerKey,
        string consumerSecret,
        AtumBatchRequest batchRequest,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://panes.gr/wp-json/wc/v3/atum/inventories/batch" +
                  $"?consumer_key={consumerKey}" +
                  $"&consumer_secret={consumerSecret}";

        _logger.LogInformation("=== ATUM BATCH UPDATE START ===");
        _logger.LogInformation("Batch URL: {Url}", url);
        _logger.LogInformation("Batch request: {CreateCount} creates, {UpdateCount} updates, {DeleteCount} deletes",
            batchRequest.Create?.Count ?? 0,
            batchRequest.Update?.Count ?? 0,
            batchRequest.Delete?.Count ?? 0);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Use JsonPropertyName attributes instead of auto-conversion
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var jsonContent = JsonSerializer.Serialize(batchRequest, jsonOptions);

        // Log as Information instead of Debug so it's always visible
        _logger.LogInformation("=== ATUM BATCH REQUEST JSON ===");
        _logger.LogInformation("{Json}", jsonContent);
        _logger.LogInformation("=== END BATCH REQUEST JSON ===");

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        _logger.LogInformation("Sending ATUM batch request to {Url}", url);

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("ATUM batch response status: {StatusCode}", response.StatusCode);
            _logger.LogDebug("ATUM batch response content: {Content}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                var batchResponse = JsonSerializer.Deserialize<AtumBatchResponse>(responseContent, jsonOptions);

                _logger.LogInformation("=== ATUM BATCH UPDATE SUCCESS ===");
                _logger.LogInformation("Batch results: {CreatedCount} created, {UpdatedCount} updated",
                    batchResponse?.Create?.Count ?? 0,
                    batchResponse?.Update?.Count ?? 0);

                return batchResponse ?? new AtumBatchResponse();
            }
            else
            {
                _logger.LogError("ATUM batch update failed with status {StatusCode}: {Content}",
                    response.StatusCode, responseContent);
                throw new HttpRequestException($"ATUM batch update failed: {response.StatusCode} - {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ATUM batch update");
            throw;
        }
    }

}