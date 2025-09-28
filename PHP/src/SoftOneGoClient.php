<?php

/**
 * SoftOne Go API Client
 * Διαχείριση επικοινωνίας με το SoftOne Go API
 *
 * Features:
 * - Connection establishment και authentication
 * - Product fetching με filters
 * - Error handling & retries με exponential backoff
 * - Rate limiting compliance
 * - Response parsing και validation
 * - Caching για performance
 */

class SoftOneGoClient {

    private $config;
    private $logger;
    private $database;
    private $rateLimit;
    private $lastRequestTime = 0;
    private $requestCount = 0;
    private $hourlyRequestCount = 0;
    private $hourlyResetTime;

    /**
     * Constructor
     *
     * @param array $storeConfig Store configuration από stores.php
     * @param Logger $logger Logger instance
     * @param PDO $database Database connection για caching
     */
    public function __construct($storeConfig, $logger, $database = null) {
        $this->config = $storeConfig['softone_go'];
        $this->logger = $logger;
        $this->database = $database;

        // Rate limiting configuration
        $appConfig = require dirname(__DIR__) . '/config/app.php';
        $this->rateLimit = $appConfig['rate_limits']['softone_go'];
        $this->hourlyResetTime = time() + 3600;

        $this->logger->info('SoftOne Go Client initialized', [
            'base_url' => $this->config['base_url'],
            'app_id' => $this->config['app_id'],
            's1code' => substr($this->config['s1code'], 0, 5) . '***' // Partial για security
        ]);
    }

    /**
     * Fetch products από SoftOne Go
     *
     * @param array $filters Additional filters (optional)
     * @return array Parsed products data
     * @throws Exception On API errors
     */
    public function fetchProducts($filters = []) {
        $startTime = microtime(true);
        $this->logger->info('Starting SoftOne Go product fetch', [
            'filters' => $filters
        ]);

        try {
            // Check rate limiting
            $this->checkRateLimit();

            // Prepare request data
            $requestData = $this->prepareRequestData($filters);

            // Check cache first
            $cacheKey = $this->generateCacheKey($requestData);
            $cachedData = $this->getFromCache($cacheKey);

            if ($cachedData) {
                $this->logger->info('Returning cached SoftOne Go data', [
                    'cache_key' => $cacheKey,
                    'cached_at' => $cachedData['cached_at']
                ]);
                return $cachedData['data'];
            }

            // Make API request
            $response = $this->makeApiRequest($requestData);

            // Parse και validate response
            $parsedData = $this->parseResponse($response);

            // Cache the response
            $this->saveToCache($cacheKey, $parsedData);

            $this->logger->logTiming('SoftOne Go API fetch', $startTime, [
                'products_count' => count($parsedData),
                'response_size' => strlen($response)
            ]);

            return $parsedData;

        } catch (Exception $e) {
            $this->logger->error('SoftOne Go API error', [
                'error' => $e->getMessage(),
                'trace' => $e->getTraceAsString()
            ]);
            throw $e;
        }
    }

    /**
     * Test connection to SoftOne Go API
     *
     * @return bool True αν η σύνδεση είναι επιτυχής
     */
    public function testConnection() {
        try {
            $this->logger->info('Testing SoftOne Go connection');

            // Make a simple request με minimal data
            $testRequestData = [
                'appId' => $this->config['app_id'],
                'token' => $this->config['token'],
                'filters' => $this->config['filters'] . '&ITEM.MTRL_ITEMTRDATA_QTY1_TO=1' // Limit to 1 product
            ];

            $response = $this->makeApiRequest($testRequestData, '/list/item');
            $parsedData = $this->parseResponse($response);

            $this->logger->info('SoftOne Go connection test successful', [
                'response_fields' => count($parsedData['fields'] ?? []),
                'test_products' => count($parsedData['rows'] ?? [])
            ]);

            return true;

        } catch (Exception $e) {
            $this->logger->error('SoftOne Go connection test failed', [
                'error' => $e->getMessage()
            ]);
            return false;
        }
    }

    /**
     * Prepare request data για API call
     *
     * @param array $additionalFilters
     * @return array
     */
    private function prepareRequestData($additionalFilters = []) {
        $filters = $this->config['filters'];

        // Add additional filters αν υπάρχουν
        if (!empty($additionalFilters)) {
            foreach ($additionalFilters as $key => $value) {
                $filters .= "&{$key}={$value}";
            }
        }

        return [
            'appId' => $this->config['app_id'],
            'token' => $this->config['token'],
            'filters' => $filters
        ];
    }

    /**
     * Make HTTP request to SoftOne Go API
     *
     * @param array $requestData
     * @param string $endpoint
     * @return string Raw response
     * @throws Exception On HTTP errors
     */
    private function makeApiRequest($requestData, $endpoint = '/list/item') {
        $url = $this->config['base_url'] . $endpoint;
        $startTime = microtime(true);

        // Initialize cURL
        $curl = curl_init();

        curl_setopt_array($curl, [
            CURLOPT_URL => $url,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_ENCODING => '',
            CURLOPT_MAXREDIRS => 10,
            CURLOPT_TIMEOUT => 30,
            CURLOPT_FOLLOWLOCATION => true,
            CURLOPT_HTTP_VERSION => CURL_HTTP_VERSION_1_1,
            CURLOPT_CUSTOMREQUEST => 'POST',
            CURLOPT_POSTFIELDS => json_encode($requestData, JSON_UNESCAPED_UNICODE),
            CURLOPT_HTTPHEADER => [
                's1code: ' . $this->config['s1code'],
                'Content-Type: application/json'
            ],
        ]);

        // Execute request με retry logic
        $response = $this->executeWithRetry($curl);
        $httpCode = curl_getinfo($curl, CURLINFO_HTTP_CODE);
        $duration = (microtime(true) - $startTime) * 1000;

        curl_close($curl);

        // Log API call
        $this->logger->logApiCall('SoftOne Go', $endpoint, 'POST', $duration, $httpCode, [
            'request_size' => strlen(json_encode($requestData)),
            'response_size' => strlen($response)
        ]);

        // Check HTTP status
        if ($httpCode !== 200) {
            throw new Exception("SoftOne Go API HTTP error: {$httpCode}. Response: " . substr($response, 0, 500));
        }

        if ($response === false) {
            throw new Exception("SoftOne Go API request failed");
        }

        // Update rate limiting counters
        $this->updateRateLimitCounters();

        return $response;
    }

    /**
     * Execute cURL με retry logic
     *
     * @param resource $curl
     * @return string
     * @throws Exception
     */
    private function executeWithRetry($curl) {
        $appConfig = require dirname(__DIR__) . '/config/app.php';
        $maxRetries = $appConfig['api_retry_attempts'] ?? 3;
        $retryDelay = $appConfig['api_retry_delay'] ?? 5;

        for ($attempt = 1; $attempt <= $maxRetries; $attempt++) {
            $response = curl_exec($curl);
            $curlError = curl_error($curl);

            if ($response !== false && empty($curlError)) {
                return $response;
            }

            $this->logger->warning("SoftOne Go API attempt {$attempt} failed", [
                'curl_error' => $curlError,
                'curl_errno' => curl_errno($curl)
            ]);

            if ($attempt < $maxRetries) {
                $sleepTime = $retryDelay * pow(2, $attempt - 1); // Exponential backoff
                $this->logger->debug("Retrying SoftOne Go API in {$sleepTime} seconds");
                sleep($sleepTime);
            }
        }

        throw new Exception("SoftOne Go API failed after {$maxRetries} attempts. Last error: {$curlError}");
    }

    /**
     * Parse API response
     *
     * @param string $response
     * @return array
     * @throws Exception
     */
    private function parseResponse($response) {
        $data = json_decode($response, true);

        if (json_last_error() !== JSON_ERROR_NONE) {
            throw new Exception("Invalid JSON response από SoftOne Go: " . json_last_error_msg());
        }

        // Validate response structure
        if (!isset($data['success'])) {
            throw new Exception("Invalid response format από SoftOne Go");
        }

        if ($data['success'] !== true) {
            throw new Exception("SoftOne Go API returned error: " . ($data['error'] ?? 'Unknown error'));
        }

        // Validate required fields
        if (!isset($data['fields']) || !isset($data['rows'])) {
            throw new Exception("Missing required fields στην SoftOne Go response");
        }

        $this->logger->info('SoftOne Go response parsed successfully', [
            'total_count' => $data['totalcount'] ?? 0,
            'fields_count' => count($data['fields']),
            'rows_count' => count($data['rows']),
            'req_id' => $data['reqID'] ?? null
        ]);

        // Transform data για easier processing
        return $this->transformResponseData($data);
    }

    /**
     * Transform SoftOne Go response σε structured format
     *
     * @param array $rawData
     * @return array
     */
    private function transformResponseData($rawData) {
        $fields = $rawData['fields'];
        $rows = $rawData['rows'];
        $products = [];

        // Create field name mapping
        $fieldMap = [];
        foreach ($fields as $index => $field) {
            $fieldMap[$index] = $field['name'];
        }

        // Transform each row
        foreach ($rows as $row) {
            $product = [];
            foreach ($row as $index => $value) {
                $fieldName = $fieldMap[$index] ?? "field_{$index}";
                $product[$fieldName] = $value;
            }

            // Add derived fields για easier access
            $product['_softone_id'] = $product['ZOOMINFO'] ?? null;
            $product['_sku'] = $product['ITEM.CODE1'] ?? $product['ITEM.CODE'] ?? null;
            $product['_name'] = $product['ITEM.NAME'] ?? null;
            $product['_stock_quantity'] = floatval($product['ITEM.MTRL_ITEMTRDATA_QTY1'] ?? 0);
            $product['_price'] = floatval($product['ITEM.PRICER'] ?? 0);

            $products[] = $product;
        }

        return [
            'metadata' => [
                'total_count' => $rawData['totalcount'] ?? count($products),
                'update_date' => $rawData['upddate'] ?? null,
                'request_id' => $rawData['reqID'] ?? null,
                'fetched_at' => date('Y-m-d H:i:s')
            ],
            'fields' => $fields,
            'products' => $products
        ];
    }

    /**
     * Check rate limiting
     *
     * @throws Exception αν rate limit exceeded
     */
    private function checkRateLimit() {
        $now = time();

        // Reset hourly counter αν needed
        if ($now >= $this->hourlyResetTime) {
            $this->hourlyRequestCount = 0;
            $this->hourlyResetTime = $now + 3600;
        }

        // Check hourly limit
        if ($this->hourlyRequestCount >= $this->rateLimit['requests_per_hour']) {
            throw new Exception("SoftOne Go hourly rate limit exceeded");
        }

        // Check minute limit
        $minuteAgo = $now - 60;
        if ($this->lastRequestTime > $minuteAgo && $this->requestCount >= $this->rateLimit['requests_per_minute']) {
            $sleepTime = 60 - ($now - $this->lastRequestTime);
            $this->logger->info("Rate limit hit, sleeping for {$sleepTime} seconds");
            sleep($sleepTime);
            $this->requestCount = 0;
        }
    }

    /**
     * Update rate limiting counters
     */
    private function updateRateLimitCounters() {
        $now = time();

        if ($now > $this->lastRequestTime + 60) {
            $this->requestCount = 1;
        } else {
            $this->requestCount++;
        }

        $this->hourlyRequestCount++;
        $this->lastRequestTime = $now;
    }

    /**
     * Generate cache key για request
     *
     * @param array $requestData
     * @return string
     */
    private function generateCacheKey($requestData) {
        return 'softone_go_' . md5(json_encode($requestData));
    }

    /**
     * Get data από cache
     *
     * @param string $cacheKey
     * @return array|null
     */
    private function getFromCache($cacheKey) {
        if (!$this->database) {
            return null;
        }

        try {
            $stmt = $this->database->prepare("
                SELECT cache_data, created_at
                FROM api_cache
                WHERE cache_key = ? AND expires_at > NOW()
                LIMIT 1
            ");

            $stmt->execute([$cacheKey]);
            $result = $stmt->fetch();

            if ($result) {
                $data = json_decode($result['cache_data'], true);
                return [
                    'data' => $data,
                    'cached_at' => $result['created_at']
                ];
            }

        } catch (Exception $e) {
            $this->logger->warning('Cache read error', ['error' => $e->getMessage()]);
        }

        return null;
    }

    /**
     * Save data to cache
     *
     * @param string $cacheKey
     * @param array $data
     */
    private function saveToCache($cacheKey, $data) {
        if (!$this->database) {
            return;
        }

        try {
            // Cache για 15 minutes
            $expiresAt = date('Y-m-d H:i:s', time() + 900);

            $stmt = $this->database->prepare("
                INSERT INTO api_cache (cache_key, cache_data, expires_at)
                VALUES (?, ?, ?)
                ON DUPLICATE KEY UPDATE
                cache_data = VALUES(cache_data),
                expires_at = VALUES(expires_at),
                created_at = NOW()
            ");

            $stmt->execute([
                $cacheKey,
                json_encode($data, JSON_UNESCAPED_UNICODE),
                $expiresAt
            ]);

        } catch (Exception $e) {
            $this->logger->warning('Cache save error', ['error' => $e->getMessage()]);
        }
    }

    /**
     * Get client statistics
     *
     * @return array
     */
    public function getStatistics() {
        return [
            'total_requests' => $this->requestCount,
            'hourly_requests' => $this->hourlyRequestCount,
            'last_request_time' => $this->lastRequestTime,
            'rate_limit_per_minute' => $this->rateLimit['requests_per_minute'],
            'rate_limit_per_hour' => $this->rateLimit['requests_per_hour']
        ];
    }
}