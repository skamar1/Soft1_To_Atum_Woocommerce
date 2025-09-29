using System.Text.Json;
using System.Text;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Blazor.Services;

public class SyncApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SyncApiClient> _logger;

    public SyncApiClient(HttpClient httpClient, ILogger<SyncApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SyncStatusResponse?> GetSyncStatusAsync()
    {
        _logger.LogDebug("Getting sync status from API");
        var response = await _httpClient.GetAsync("/api/sync/status");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SyncStatusResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<List<SyncLogResponse>?> GetSyncLogsAsync()
    {
        var response = await _httpClient.GetAsync("/api/sync/logs");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<SyncLogResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<ProductsPageResponse?> GetProductsAsync(int page = 1, int pageSize = 1000)
    {
        var response = await _httpClient.GetAsync($"/api/products?page={page}&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ProductsPageResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<List<ProductResponse>?> GetAllProductsAsync()
    {
        var allProducts = new List<ProductResponse>();
        int page = 1;
        const int pageSize = 100;
        bool hasMoreData = true;

        _logger.LogDebug("Loading all products from API with pagination");

        while (hasMoreData)
        {
            try
            {
                var response = await GetProductsAsync(page, pageSize);
                if (response?.Products?.Any() == true)
                {
                    allProducts.AddRange(response.Products);
                    hasMoreData = response.Products.Count == pageSize && page < response.TotalPages;
                    page++;
                }
                else
                {
                    hasMoreData = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products page {Page}", page);
                throw;
            }
        }

        _logger.LogDebug("Loaded {TotalProducts} products from API", allProducts.Count);
        return allProducts;
    }

    public async Task<List<StoreResponse>?> GetStoresAsync()
    {
        var response = await _httpClient.GetAsync("/api/stores");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<StoreResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<ManualSyncResponse?> StartManualSyncAsync()
    {
        try
        {
            _logger.LogInformation("=== MANUAL SYNC CLIENT REQUEST START ===");
            _logger.LogInformation("HTTP Client BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("Starting manual sync API call to /api/sync/manual");

            var response = await _httpClient.PostAsync("/api/sync/manual", null);
            _logger.LogInformation("Manual sync API response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Manual sync API response content: {Content}", content);

                var result = JsonSerializer.Deserialize<ManualSyncResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Manual sync API call completed successfully. SyncLogId: {SyncLogId}", result?.SyncLogId);
                _logger.LogInformation("=== MANUAL SYNC CLIENT REQUEST SUCCESS ===");
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Manual sync API failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Manual sync failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during manual sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Failed to connect to API service at {_httpClient.BaseAddress}. Error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during manual sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Request timed out connecting to API service at {_httpClient.BaseAddress}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during manual sync request");
            throw new Exception($"Unexpected error during manual sync: {ex.Message}", ex);
        }
    }

    public async Task<ManualSyncResponse?> StartAtumSyncAsync()
    {
        try
        {
            _logger.LogInformation("=== ATUM SYNC CLIENT REQUEST START ===");
            _logger.LogInformation("HTTP Client BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("Starting ATUM sync API call to /api/sync/atum");

            var response = await _httpClient.PostAsync("/api/sync/atum", null);
            _logger.LogInformation("ATUM sync API response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("ATUM sync API response content: {Content}", content);

                var result = JsonSerializer.Deserialize<ManualSyncResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("ATUM sync API call completed successfully. SyncLogId: {SyncLogId}", result?.SyncLogId);
                _logger.LogInformation("=== ATUM SYNC CLIENT REQUEST SUCCESS ===");
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("ATUM sync API failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"ATUM sync failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during ATUM sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Failed to connect to API service at {_httpClient.BaseAddress}. Error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during ATUM sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Request timed out connecting to API service at {_httpClient.BaseAddress}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ATUM sync request");
            throw new Exception($"Unexpected error during ATUM sync: {ex.Message}", ex);
        }
    }

    public async Task<ApiSettingsModel?> GetSettingsAsync()
    {
        try
        {
            _logger.LogInformation("=== GET SETTINGS REQUEST ===");
            Console.WriteLine("=== GET SETTINGS REQUEST ===");
            _logger.LogInformation("Fetching settings from API: {BaseAddress}api/settings", _httpClient.BaseAddress);
            Console.WriteLine($"Fetching settings from API: {_httpClient.BaseAddress}api/settings");

            var response = await _httpClient.GetAsync("/api/settings");
            _logger.LogInformation("Settings API response: {StatusCode}", response.StatusCode);
            Console.WriteLine($"Settings API response: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Settings retrieved successfully");
                Console.WriteLine("Settings retrieved successfully");

                return JsonSerializer.Deserialize<ApiSettingsModel>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get settings: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                Console.WriteLine($"Failed to get settings: {response.StatusCode} - {errorContent}");
                throw new HttpRequestException($"Failed to get settings: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting settings from API");
            Console.WriteLine($"Error getting settings from API: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> UpdateSettingsAsync(ApiSettingsModel settings)
    {
        try
        {
            _logger.LogInformation("=== UPDATE SETTINGS REQUEST ===");
            _logger.LogInformation("Updating settings via API: {BaseAddress}api/settings", _httpClient.BaseAddress);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            _logger.LogDebug("Settings JSON payload: {Json}", json);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync("/api/settings", content);

            _logger.LogInformation("Update settings API response: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Settings updated successfully");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update settings: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings via API");
            return false;
        }
    }

    public async Task<ConnectionTestResponse> TestConnectionAsync(string service)
    {
        try
        {
            _logger.LogInformation("=== TEST CONNECTION REQUEST ===");
            _logger.LogInformation("Testing {Service} connection via API: {BaseAddress}api/settings/test/{Service}", service, _httpClient.BaseAddress, service);

            var response = await _httpClient.GetAsync($"/api/settings/test/{service}");
            _logger.LogInformation("Test connection API response for {Service}: {StatusCode}", service, response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Connection test for {Service} successful: {Content}", service, content);

                // Parse the JSON response to get the message
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
                var message = jsonResponse.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? $"{service.ToUpper()} connection successful"
                    : $"{service.ToUpper()} connection successful";

                return new ConnectionTestResponse { Success = true, Message = message };
            }
            else
            {
                _logger.LogWarning("Connection test for {Service} failed: {StatusCode} - {ErrorContent}", service, response.StatusCode, content);

                // Try to parse error message from JSON response
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
                    var message = jsonResponse.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString() ?? $"{service.ToUpper()} connection failed"
                        : $"{service.ToUpper()} connection failed";

                    return new ConnectionTestResponse { Success = false, Message = message };
                }
                catch
                {
                    return new ConnectionTestResponse { Success = false, Message = $"{service.ToUpper()} connection failed - {response.StatusCode}" };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing {Service} connection", service);
            return new ConnectionTestResponse { Success = false, Message = $"Error testing {service} connection: {ex.Message}" };
        }
    }

    public async Task<ConnectionTestResponse> TestEmailAsync()
    {
        try
        {
            _logger.LogInformation("=== TEST EMAIL REQUEST ===");
            _logger.LogInformation("Testing email configuration via API: {BaseAddress}api/settings/test/email", _httpClient.BaseAddress);

            var response = await _httpClient.GetAsync("/api/settings/test/email");
            _logger.LogInformation("Test email API response: {StatusCode}", response.StatusCode);

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email test successful: {Content}", content);

                // Parse the JSON response to get the message
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
                var message = jsonResponse.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? "Test email sent successfully"
                    : "Test email sent successfully";

                return new ConnectionTestResponse { Success = true, Message = message };
            }
            else
            {
                _logger.LogWarning("Email test failed: {StatusCode} - {ErrorContent}", response.StatusCode, content);

                // Try to parse error message from JSON response
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
                    var message = jsonResponse.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString() ?? "Failed to send test email"
                        : "Failed to send test email";

                    return new ConnectionTestResponse { Success = false, Message = message };
                }
                catch
                {
                    return new ConnectionTestResponse { Success = false, Message = $"Failed to send test email - {response.StatusCode}" };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing email configuration");
            return new ConnectionTestResponse { Success = false, Message = $"Error testing email: {ex.Message}" };
        }
    }

    public async Task<ProductStatisticsResponse?> GetProductStatisticsAsync()
    {
        try
        {
            _logger.LogInformation("=== GET PRODUCT STATISTICS REQUEST ===");
            _logger.LogInformation("Fetching product statistics from API: {BaseAddress}api/products/statistics", _httpClient.BaseAddress);

            var response = await _httpClient.GetAsync("/api/products/statistics");
            _logger.LogInformation("Product statistics API response: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Product statistics retrieved successfully");

                return JsonSerializer.Deserialize<ProductStatisticsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get product statistics: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product statistics from API");
            return null;
        }
    }

    public async Task<ManualSyncResponse?> StartAtumBatchSyncAsync()
    {
        try
        {
            _logger.LogInformation("=== ATUM BATCH SYNC CLIENT REQUEST START ===");
            _logger.LogInformation("HTTP Client BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("Starting ATUM batch sync API call to /api/sync/atum-batch");

            var response = await _httpClient.PostAsync("/api/sync/atum-batch", null);
            _logger.LogInformation("ATUM batch sync API response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("ATUM batch sync API response content: {Content}", content);

                var result = JsonSerializer.Deserialize<ManualSyncResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("ATUM batch sync API call completed successfully. SyncLogId: {SyncLogId}", result?.SyncLogId);
                _logger.LogInformation("=== ATUM BATCH SYNC CLIENT REQUEST SUCCESS ===");
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("ATUM batch sync API failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"ATUM batch sync failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during ATUM batch sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Failed to connect to API service at {_httpClient.BaseAddress}. Error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during ATUM batch sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Request timed out connecting to API service at {_httpClient.BaseAddress}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ATUM batch sync request");
            throw new Exception($"Unexpected error during ATUM batch sync: {ex.Message}", ex);
        }
    }

    public async Task<ManualSyncResponse?> StartWooCommerceSyncAsync()
    {
        try
        {
            _logger.LogInformation("=== WOOCOMMERCE SYNC CLIENT REQUEST START ===");
            _logger.LogInformation("HTTP Client BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
            _logger.LogInformation("Starting WooCommerce sync API call to /api/sync/woocommerce");

            var response = await _httpClient.PostAsync("/api/sync/woocommerce", null);
            _logger.LogInformation("WooCommerce sync API response status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("WooCommerce sync API response content: {Content}", content);

                var result = JsonSerializer.Deserialize<ManualSyncResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("WooCommerce sync API call completed successfully");
                _logger.LogInformation("=== WOOCOMMERCE SYNC CLIENT REQUEST SUCCESS ===");
                return result;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("WooCommerce sync API failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"WooCommerce sync failed: {response.StatusCode} - {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during WooCommerce sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Failed to connect to API service at {_httpClient.BaseAddress}. Error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during WooCommerce sync request to {BaseAddress}", _httpClient.BaseAddress);
            throw new Exception($"Request timed out connecting to API service at {_httpClient.BaseAddress}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during WooCommerce sync request");
            throw new Exception($"Unexpected error during WooCommerce sync: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteAllProductsAsync()
    {
        try
        {
            _logger.LogWarning("=== DELETE ALL PRODUCTS REQUEST ===");
            _logger.LogWarning("⚠️ DANGER: Requesting deletion of ALL products from database");

            var response = await _httpClient.DeleteAsync("/api/products/all");
            _logger.LogInformation("Delete all products API response: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Delete all products successful: {Content}", content);

                // Parse the JSON response to get details
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
                var deletedCount = jsonResponse.TryGetProperty("deletedCount", out var countElement)
                    ? countElement.GetInt32()
                    : 0;

                _logger.LogWarning("Successfully deleted {DeletedCount} products from database", deletedCount);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Delete all products failed: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all products");
            return false;
        }
    }
}
