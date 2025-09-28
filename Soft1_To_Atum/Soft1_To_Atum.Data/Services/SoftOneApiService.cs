using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Soft1_To_Atum.Data.Models;

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
                try
                {
                    var errorBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var contentType = response.Content.Headers.ContentType?.ToString();
                    var errorContent = DecodeResponseContent(errorBytes, contentType);
                    _logger.LogWarning("SoftOne API test failed. Status: {StatusCode}, Error: {Error}",
                        response.StatusCode, errorContent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SoftOne API test failed. Status: {StatusCode}, could not read error content",
                        response.StatusCode);
                }
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

    public async Task<List<SoftOneProduct>> GetProductsAsync(string baseUrl, string appId, string token, string s1Code, string filters, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching products from SoftOne with filters: {Filters}", filters);

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

            // Read response as bytes to handle encoding issues
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString();
            _logger.LogDebug("Received response from SoftOne API: {Length} bytes, ContentType: {ContentType}",
                responseBytes.Length, contentType);

            // Decode using appropriate encoding
            var responseContent = DecodeResponseContent(responseBytes, contentType);
            _logger.LogDebug("Successfully decoded response: {Length} characters", responseContent.Length);

            SoftOneApiResponse? apiResponse;
            try
            {
                apiResponse = JsonSerializer.Deserialize<SoftOneApiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize SoftOne API response. Raw response: {Response}",
                    responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent);
                throw new InvalidOperationException("Invalid JSON response from SoftOne API", ex);
            }

            if (apiResponse == null)
            {
                _logger.LogError("SoftOne API response deserialized to null. Raw response: {Response}",
                    responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent);
                throw new InvalidOperationException("Null response from SoftOne API");
            }

            if (!apiResponse.Success)
            {
                _logger.LogWarning("SoftOne API returned unsuccessful response. Success: {Success}, TotalCount: {TotalCount}",
                    apiResponse.Success, apiResponse.TotalCount);
                return [];
            }

            var products = new List<SoftOneProduct>();
            foreach (var row in apiResponse.Rows)
            {
                try
                {
                    var product = SoftOneProduct.FromApiRow(row, apiResponse.Fields);
                    products.Add(product);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse SoftOne product row");
                }
            }

            _logger.LogInformation("Successfully parsed {ProductCount} products from SoftOne API (Total: {TotalCount})",
                products.Count, apiResponse.TotalCount);

            return products;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize SoftOne API response");
            throw new InvalidOperationException("Invalid response format from SoftOne API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products from SoftOne API: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Decodes response content using appropriate encoding, handling Greek characters (windows-1253)
    /// </summary>
    private string DecodeResponseContent(byte[] responseBytes, string? contentType = null)
    {
        try
        {
            // First, try windows-1253 if it's specified in content type or if we detect Greek content
            if (!string.IsNullOrEmpty(contentType) && contentType.Contains("windows-1253"))
            {
                _logger.LogDebug("Detected windows-1253 encoding in content type, using appropriate decoder");

                // Register the encoding provider for windows-1253
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var greekEncoding = Encoding.GetEncoding("windows-1253");
                var decoded = greekEncoding.GetString(responseBytes);
                _logger.LogDebug("Successfully decoded response using windows-1253 encoding");
                return decoded;
            }

            // Try UTF-8 first (most common)
            try
            {
                var utf8Result = Encoding.UTF8.GetString(responseBytes);
                _logger.LogDebug("Successfully decoded response using UTF-8 encoding");
                return utf8Result;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UTF-8 decoding failed, trying windows-1253");
            }

            // Fallback to windows-1253 for Greek content
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var greekEncoding = Encoding.GetEncoding("windows-1253");
                var greekResult = greekEncoding.GetString(responseBytes);
                _logger.LogDebug("Successfully decoded response using windows-1253 fallback");
                return greekResult;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Windows-1253 decoding failed, trying ASCII as last resort");
            }

            // Last resort: ASCII
            var asciiResult = Encoding.ASCII.GetString(responseBytes);
            _logger.LogWarning("Used ASCII encoding as last resort - some characters may be corrupted");
            return asciiResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All encoding attempts failed, returning raw bytes as UTF-8");
            return Encoding.UTF8.GetString(responseBytes);
        }
    }
}