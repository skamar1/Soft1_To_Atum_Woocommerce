using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Soft1_To_Atum.Data.Services;

public class SoftOneApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SoftOneApiService> _logger;

    public SoftOneApiService(HttpClient httpClient, ILogger<SoftOneApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(string baseUrl, string appId, string token, string s1Code, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing SoftOne connection to {BaseUrl} with AppId {AppId}", baseUrl, appId);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/list/item");
            request.Headers.Add("s1code", s1Code);

            var requestBody = new
            {
                appId = appId,
                filters = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=1", // Small test query
                token = token
            };

            var json = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending test request to SoftOne API");
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    // Try to read as bytes first to avoid encoding issues
                    var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    _logger.LogDebug("SoftOne API test successful. Response length: {Length} bytes", responseBytes.Length);
                    return true;
                }
                catch (Exception readEx)
                {
                    _logger.LogWarning(readEx, "Could not read response content, but got success status code. Treating as success.");
                    return true; // If we got HTTP 200, the connection is working
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("SoftOne API test failed. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during SoftOne API test: {Message}", ex.Message);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during SoftOne API test: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SoftOne API test: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<string> GetItemsAsync(string baseUrl, string appId, string token, string s1Code, string filters, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching items from SoftOne with filters: {Filters}", filters);

            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/list/item");
            request.Headers.Add("s1code", s1Code);

            var requestBody = new
            {
                appId = appId,
                filters = filters,
                token = token
            };

            var json = JsonSerializer.Serialize(requestBody);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Successfully fetched {Length} characters from SoftOne API", responseContent.Length);

            return responseContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching items from SoftOne API: {Message}", ex.Message);
            throw;
        }
    }
}