<?php

/**
 * WooCommerce & ATUM API Client
 * Διαχείριση επικοινωνίας με WooCommerce και ATUM Multi Inventory APIs
 *
 * Features:
 * - WooCommerce API connection
 * - ATUM inventory fetching και management
 * - Product search by SKU/Barcode
 * - Product creation στο WooCommerce
 * - Batch inventory operations (create/update/delete)
 * - Pagination handling για large datasets
 * - Error handling με retry logic
 */

class WooCommerceClient {

    private $config;
    private $logger;
    private $database;
    private $baseUrl;
    private $authParams;
    private $rateLimit;
    private $lastRequestTime = 0;
    private $requestCount = 0;

    /**
     * Constructor
     *
     * @param array $storeConfig Store configuration από stores.php
     * @param Logger $logger Logger instance
     * @param PDO $database Database connection
     */
    public function __construct($storeConfig, $logger, $database = null) {
        $this->config = $storeConfig['woocommerce'];
        $this->logger = $logger;
        $this->database = $database;

        $this->baseUrl = rtrim($this->config['url'], '/') . '/wp-json/';
        $this->authParams = [
            'consumer_key' => $this->config['consumer_key'],
            'consumer_secret' => $this->config['consumer_secret']
        ];

        // Rate limiting configuration
        $appConfig = require dirname(__DIR__) . '/config/app.php';
        $this->rateLimit = $appConfig['rate_limits']['woocommerce'];

        $this->logger->info('WooCommerce Client initialized', [
            'base_url' => $this->baseUrl,
            'consumer_key' => substr($this->config['consumer_key'], 0, 10) . '***'
        ]);
    }

    /**
     * Test WooCommerce API connection
     *
     * @return bool
     */
    public function testConnection() {
        try {
            $this->logger->info('Testing WooCommerce connection');

            $response = $this->makeRequest('GET', 'wc/v3/system_status', [], false);

            $this->logger->info('WooCommerce connection test successful', [
                'environment' => $response['environment']['wp_version'] ?? 'unknown'
            ]);

            return true;

        } catch (Exception $e) {
            $this->logger->error('WooCommerce connection test failed', [
                'error' => $e->getMessage()
            ]);
            return false;
        }
    }

    /**
     * Get ATUM inventory για specific location
     *
     * @param int $locationId ATUM location ID
     * @param int $page Page number για pagination
     * @param int $perPage Items per page
     * @return array
     */
    public function getAtumInventory($locationId, $page = 1, $perPage = 100) {
        $startTime = microtime(true);

        $this->logger->info('Fetching ATUM inventory', [
            'location_id' => $locationId,
            'page' => $page,
            'per_page' => $perPage
        ]);

        try {
            $params = [
                'location' => $locationId,
                'page' => $page,
                'per_page' => $perPage
            ];

            $response = $this->makeRequest('GET', 'wc/v3/atum/inventories', $params);

            $this->logger->logTiming('ATUM inventory fetch', $startTime, [
                'location_id' => $locationId,
                'items_count' => count($response),
                'page' => $page
            ]);

            return $response;

        } catch (Exception $e) {
            $this->logger->error('ATUM inventory fetch failed', [
                'location_id' => $locationId,
                'error' => $e->getMessage()
            ]);
            throw $e;
        }
    }

    /**
     * Search for products by SKU ή barcode
     *
     * @param string $sku
     * @return array|null Product data ή null αν δεν βρεθεί
     */
    public function findProductBySku($sku) {
        try {
            $this->logger->debug('Searching product by SKU', ['sku' => $sku]);

            $params = [
                'sku' => $sku,
                'per_page' => 1
            ];

            $response = $this->makeRequest('GET', 'wc/v3/products', $params);

            if (!empty($response) && is_array($response)) {
                $product = $response[0];
                $this->logger->debug('Product found by SKU', [
                    'sku' => $sku,
                    'product_id' => $product['id'],
                    'name' => $product['name']
                ]);
                return $product;
            }

            $this->logger->debug('Product not found by SKU', ['sku' => $sku]);
            return null;

        } catch (Exception $e) {
            $this->logger->warning('Product search by SKU failed', [
                'sku' => $sku,
                'error' => $e->getMessage()
            ]);
            return null;
        }
    }

    /**
     * Create new product στο WooCommerce
     *
     * @param array $productData Product data από SoftOne Go
     * @param array $storeConfig Store configuration για field mapping
     * @return array Created product data
     */
    public function createProduct($productData, $storeConfig) {
        $startTime = microtime(true);

        try {
            $this->logger->info('Creating WooCommerce product', [
                'softone_name' => $productData['_name'],
                'softone_sku' => $productData['_sku']
            ]);

            // Map SoftOne Go data to WooCommerce format
            $wooProduct = $this->mapSoftOneToWooCommerce($productData, $storeConfig);

            $response = $this->makeRequest('POST', 'wc/v3/products', $wooProduct);

            $this->logger->logTiming('WooCommerce product creation', $startTime, [
                'product_id' => $response['id'],
                'sku' => $response['sku'],
                'name' => $response['name']
            ]);

            return $response;

        } catch (Exception $e) {
            $this->logger->error('WooCommerce product creation failed', [
                'product_data' => $productData,
                'error' => $e->getMessage()
            ]);
            throw $e;
        }
    }

    /**
     * Batch operations για ATUM inventory
     *
     * @param array $operations Array με create, update, delete operations
     * @return array Batch response
     */
    public function batchAtumInventory($operations) {
        $startTime = microtime(true);

        $this->logger->info('Executing ATUM batch operations', [
            'create_count' => count($operations['create'] ?? []),
            'update_count' => count($operations['update'] ?? []),
            'delete_count' => count($operations['delete'] ?? [])
        ]);

        try {
            $response = $this->makeRequest('POST', 'wc/v3/atum/inventories/batch', $operations);

            $this->logger->logTiming('ATUM batch operations', $startTime, [
                'created' => count($response['create'] ?? []),
                'updated' => count($response['update'] ?? []),
                'deleted' => count($response['delete'] ?? [])
            ]);

            return $response;

        } catch (Exception $e) {
            $this->logger->error('ATUM batch operations failed', [
                'operations' => $operations,
                'error' => $e->getMessage()
            ]);
            throw $e;
        }
    }

    /**
     * Create ATUM inventory entry
     *
     * @param int $productId WooCommerce product ID
     * @param array $inventoryData Inventory data
     * @param array $atumConfig ATUM configuration
     * @return array
     */
    public function createAtumInventory($productId, $inventoryData, $atumConfig) {
        $inventoryEntry = [
            'product_id' => $productId,
            'name' => $atumConfig['location_name'],
            'is_main' => false,
            'location' => [$atumConfig['location_id']],
            'meta_data' => [
                'sku' => $inventoryData['sku'],
                'manage_stock' => true,
                'stock_quantity' => intval($inventoryData['stock_quantity']),
                'backorders' => 'no',
                'stock_status' => $inventoryData['stock_quantity'] > 0 ? 'instock' : 'outofstock',
                'barcode' => $inventoryData['barcode'] ?? ''
            ]
        ];

        $this->logger->debug('Creating ATUM inventory entry', [
            'product_id' => $productId,
            'location_id' => $atumConfig['location_id'],
            'stock_quantity' => $inventoryData['stock_quantity']
        ]);

        return $inventoryEntry;
    }

    /**
     * Update ATUM inventory entry
     *
     * @param int $inventoryId ATUM inventory ID
     * @param array $inventoryData Updated inventory data
     * @return array
     */
    public function updateAtumInventory($inventoryId, $inventoryData) {
        $updateData = [
            'id' => $inventoryId,
            'meta_data' => [
                'manage_stock' => true,
                'stock_quantity' => intval($inventoryData['stock_quantity']),
                'stock_status' => $inventoryData['stock_quantity'] > 0 ? 'instock' : 'outofstock'
            ]
        ];

        $this->logger->debug('Updating ATUM inventory entry', [
            'inventory_id' => $inventoryId,
            'new_quantity' => $inventoryData['stock_quantity']
        ]);

        return $updateData;
    }

    /**
     * Get all ATUM inventory για location με pagination
     *
     * @param int $locationId
     * @return array Complete inventory list
     */
    public function getAllAtumInventory($locationId) {
        $allInventory = [];
        $page = 1;
        $perPage = 100;

        $this->logger->info('Fetching all ATUM inventory', ['location_id' => $locationId]);

        do {
            $inventory = $this->getAtumInventory($locationId, $page, $perPage);

            if (empty($inventory)) {
                break;
            }

            $allInventory = array_merge($allInventory, $inventory);
            $page++;

            $this->logger->debug("Fetched page {$page}", [
                'items_this_page' => count($inventory),
                'total_items' => count($allInventory)
            ]);

            // Rate limiting
            $this->respectRateLimit();

        } while (count($inventory) === $perPage);

        $this->logger->info('Completed ATUM inventory fetch', [
            'location_id' => $locationId,
            'total_items' => count($allInventory),
            'total_pages' => $page - 1
        ]);

        return $allInventory;
    }

    /**
     * Map SoftOne Go product data to WooCommerce format
     *
     * @param array $softOneProduct
     * @param array $storeConfig
     * @return array
     */
    private function mapSoftOneToWooCommerce($softOneProduct, $storeConfig) {
        $mapping = $storeConfig['field_mapping'];

        $product = [
            'name' => $softOneProduct[$mapping['name']] ?? 'Unnamed Product',
            'type' => 'simple',
            'regular_price' => strval($softOneProduct[$mapping['price']] ?? '0'),
            'description' => '',
            'short_description' => '',
            'sku' => $softOneProduct[$mapping['sku']] ?? '',
            'manage_stock' => true,
            'stock_quantity' => intval($softOneProduct[$mapping['stock_quantity']] ?? 0),
            'in_stock' => intval($softOneProduct[$mapping['stock_quantity']] ?? 0) > 0,
            'status' => 'publish',
            'catalog_visibility' => 'visible',
            'meta_data' => [
                [
                    'key' => '_softone_item_id',
                    'value' => $softOneProduct['ZOOMINFO'] ?? ''
                ],
                [
                    'key' => '_softone_sync_date',
                    'value' => date('Y-m-d H:i:s')
                ]
            ]
        ];

        // Add categories αν available
        if (!empty($softOneProduct[$mapping['category']])) {
            $categoryName = $softOneProduct[$mapping['category']];
            $product['categories'] = [['name' => $categoryName]];
        }

        return $product;
    }

    /**
     * Make HTTP request to WooCommerce API
     *
     * @param string $method HTTP method
     * @param string $endpoint API endpoint
     * @param array $data Request data
     * @param bool $includeAuth Include authentication
     * @return array Response data
     * @throws Exception
     */
    private function makeRequest($method, $endpoint, $data = [], $includeAuth = true) {
        $this->respectRateLimit();

        $url = $this->baseUrl . $endpoint;
        $startTime = microtime(true);

        // Add authentication params for GET requests
        if ($method === 'GET' && $includeAuth) {
            $queryParams = array_merge($data, $this->authParams);
            $url .= '?' . http_build_query($queryParams);
            $data = [];
        }

        // Initialize cURL
        $curl = curl_init();
        $headers = ['Content-Type: application/json'];

        $curlOptions = [
            CURLOPT_URL => $url,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 30,
            CURLOPT_HTTPHEADER => $headers,
            CURLOPT_USERAGENT => 'SoftOne-ATUM-Sync/1.0'
        ];

        // Set method-specific options
        switch ($method) {
            case 'POST':
                $curlOptions[CURLOPT_POST] = true;
                if ($includeAuth) {
                    $url .= '?' . http_build_query($this->authParams);
                    $curlOptions[CURLOPT_URL] = $url;
                }
                if (!empty($data)) {
                    $curlOptions[CURLOPT_POSTFIELDS] = json_encode($data, JSON_UNESCAPED_UNICODE);
                }
                break;

            case 'PUT':
                $curlOptions[CURLOPT_CUSTOMREQUEST] = 'PUT';
                if (!empty($data)) {
                    $curlOptions[CURLOPT_POSTFIELDS] = json_encode($data, JSON_UNESCAPED_UNICODE);
                }
                break;

            case 'DELETE':
                $curlOptions[CURLOPT_CUSTOMREQUEST] = 'DELETE';
                break;
        }

        curl_setopt_array($curl, $curlOptions);

        // Execute με retry logic
        $response = $this->executeWithRetry($curl, $method, $endpoint);
        $httpCode = curl_getinfo($curl, CURLINFO_HTTP_CODE);
        $duration = (microtime(true) - $startTime) * 1000;

        curl_close($curl);

        // Log API call
        $this->logger->logApiCall('WooCommerce', $endpoint, $method, $duration, $httpCode, [
            'request_size' => !empty($data) ? strlen(json_encode($data)) : 0,
            'response_size' => strlen($response)
        ]);

        return $this->parseResponse($response, $httpCode);
    }

    /**
     * Execute cURL με retry logic
     *
     * @param resource $curl
     * @param string $method
     * @param string $endpoint
     * @return string
     * @throws Exception
     */
    private function executeWithRetry($curl, $method, $endpoint) {
        $appConfig = require dirname(__DIR__) . '/config/app.php';
        $maxRetries = $appConfig['api_retry_attempts'] ?? 3;
        $retryDelay = $appConfig['api_retry_delay'] ?? 5;

        for ($attempt = 1; $attempt <= $maxRetries; $attempt++) {
            $response = curl_exec($curl);
            $curlError = curl_error($curl);
            $httpCode = curl_getinfo($curl, CURLINFO_HTTP_CODE);

            // Success conditions
            if ($response !== false && empty($curlError) && $httpCode < 500) {
                $this->updateRateLimitCounters();
                return $response;
            }

            // Log retry attempt
            $this->logger->warning("WooCommerce API attempt {$attempt} failed", [
                'method' => $method,
                'endpoint' => $endpoint,
                'http_code' => $httpCode,
                'curl_error' => $curlError
            ]);

            if ($attempt < $maxRetries) {
                $sleepTime = $retryDelay * pow(2, $attempt - 1);
                $this->logger->debug("Retrying WooCommerce API in {$sleepTime} seconds");
                sleep($sleepTime);
            }
        }

        throw new Exception("WooCommerce API failed after {$maxRetries} attempts. Last HTTP code: {$httpCode}, cURL error: {$curlError}");
    }

    /**
     * Parse API response
     *
     * @param string $response
     * @param int $httpCode
     * @return array
     * @throws Exception
     */
    private function parseResponse($response, $httpCode) {
        if ($httpCode >= 400) {
            $errorData = json_decode($response, true);
            $errorMessage = $errorData['message'] ?? "HTTP {$httpCode} error";
            throw new Exception("WooCommerce API error: {$errorMessage}");
        }

        $data = json_decode($response, true);

        if (json_last_error() !== JSON_ERROR_NONE) {
            throw new Exception("Invalid JSON response από WooCommerce: " . json_last_error_msg());
        }

        return $data;
    }

    /**
     * Respect rate limiting
     */
    private function respectRateLimit() {
        $now = time();

        if ($this->lastRequestTime > 0) {
            $timeSinceLastRequest = $now - $this->lastRequestTime;
            $minInterval = 60 / $this->rateLimit['requests_per_minute'];

            if ($timeSinceLastRequest < $minInterval) {
                $sleepTime = $minInterval - $timeSinceLastRequest;
                usleep($sleepTime * 1000000); // Convert to microseconds
            }
        }
    }

    /**
     * Update rate limiting counters
     */
    private function updateRateLimitCounters() {
        $this->lastRequestTime = time();
        $this->requestCount++;
    }

    /**
     * Get client statistics
     *
     * @return array
     */
    public function getStatistics() {
        return [
            'total_requests' => $this->requestCount,
            'last_request_time' => $this->lastRequestTime,
            'rate_limit_per_minute' => $this->rateLimit['requests_per_minute'],
            'rate_limit_per_hour' => $this->rateLimit['requests_per_hour']
        ];
    }
}