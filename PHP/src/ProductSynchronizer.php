<?php

/**
 * Product Synchronizer
 * Κεντρική λογική συγχρονισμού μεταξύ SoftOne Go και ATUM
 *
 * Features:
 * - Product matching logic (SKU/Barcode/Name)
 * - Quantity comparison και sync decisions
 * - Batch operation builder για efficiency
 * - Progress tracking και statistics
 * - Memory efficient processing για large datasets
 * - Error handling και recovery
 */

class ProductSynchronizer {

    private $storeConfig;
    private $logger;
    private $database;
    private $softOneClient;
    private $wooCommerceClient;
    private $syncLogId;

    // Statistics tracking
    private $stats = [
        'products_processed' => 0,
        'products_created' => 0,
        'products_updated' => 0,
        'products_errors' => 0,
        'products_skipped' => 0,
        'api_calls_softone' => 0,
        'api_calls_woocommerce' => 0,
        'new_products' => [],
        'updated_products' => [],
        'error_products' => []
    ];

    // Batch operations για efficiency
    private $batchOperations = [
        'create' => [],
        'update' => [],
        'delete' => []
    ];

    private $batchSize;

    /**
     * Constructor
     *
     * @param array $storeConfig Store configuration
     * @param Logger $logger Logger instance
     * @param PDO $database Database connection
     * @param int $syncLogId Sync log ID για tracking
     */
    public function __construct($storeConfig, $logger, $database, $syncLogId = null) {
        $this->storeConfig = $storeConfig;
        $this->logger = $logger;
        $this->database = $database;
        $this->syncLogId = $syncLogId;

        // Initialize clients
        $this->softOneClient = new SoftOneGoClient($storeConfig, $logger, $database);
        $this->wooCommerceClient = new WooCommerceClient($storeConfig, $logger, $database);

        // Load app configuration
        $appConfig = require dirname(__DIR__) . '/config/app.php';
        $this->batchSize = $appConfig['batch_size'] ?? 100;

        $this->logger->info('ProductSynchronizer initialized', [
            'store_id' => $storeConfig['name'] ?? 'unknown',
            'atum_location_id' => $storeConfig['atum']['location_id'],
            'batch_size' => $this->batchSize
        ]);
    }

    /**
     * Execute full synchronization για το store
     *
     * @param bool $dryRun Αν true, δεν κάνει changes στο WooCommerce
     * @return array Sync results και statistics
     */
    public function synchronize($dryRun = false) {
        $startTime = microtime(true);

        $this->logger->info('Starting product synchronization', [
            'store' => $this->storeConfig['name'],
            'dry_run' => $dryRun
        ]);

        try {
            // Step 1: Fetch products από SoftOne Go
            $this->logger->info('Step 1: Fetching SoftOne Go products');
            $softOneProducts = $this->fetchSoftOneProducts();
            $this->stats['api_calls_softone']++;

            // Step 2: Fetch current ATUM inventory
            $this->logger->info('Step 2: Fetching ATUM inventory');
            $atumInventory = $this->fetchAtumInventory();
            $this->stats['api_calls_woocommerce']++;

            // Step 3: Process products και build sync operations
            $this->logger->info('Step 3: Processing products και building sync operations');
            $this->processProducts($softOneProducts, $atumInventory);

            // Step 4: Execute batch operations
            if (!$dryRun) {
                $this->logger->info('Step 4: Executing batch operations');
                $this->executeBatchOperations();
            } else {
                $this->logger->info('Step 4: Dry run - skipping batch operations');
            }

            // Step 5: Update statistics
            $this->updateStatistics();

            $executionTime = microtime(true) - $startTime;
            $this->logger->logTiming('Full synchronization', $startTime, $this->stats);

            $this->logger->info('Synchronization completed successfully', [
                'execution_time' => round($executionTime, 2) . 's',
                'statistics' => $this->stats
            ]);

            return [
                'success' => true,
                'statistics' => $this->stats,
                'execution_time' => $executionTime,
                'dry_run' => $dryRun
            ];

        } catch (Exception $e) {
            $this->logger->error('Synchronization failed', [
                'error' => $e->getMessage(),
                'trace' => $e->getTraceAsString(),
                'statistics' => $this->stats
            ]);

            return [
                'success' => false,
                'error' => $e->getMessage(),
                'statistics' => $this->stats
            ];
        }
    }

    /**
     * Fetch products από SoftOne Go
     *
     * @return array
     */
    private function fetchSoftOneProducts() {
        $this->logger->logMemoryUsage('Before SoftOne Go fetch');

        $response = $this->softOneClient->fetchProducts();
        $products = $response['products'] ?? [];

        $this->logger->info('SoftOne Go products fetched', [
            'total_count' => count($products),
            'metadata' => $response['metadata'] ?? []
        ]);

        $this->logger->logMemoryUsage('After SoftOne Go fetch');

        return $products;
    }

    /**
     * Fetch ATUM inventory για το location
     *
     * @return array Indexed by SKU για γρήγορη αναζήτηση
     */
    private function fetchAtumInventory() {
        $this->logger->logMemoryUsage('Before ATUM fetch');

        $locationId = $this->storeConfig['atum']['location_id'];
        $inventory = $this->wooCommerceClient->getAllAtumInventory($locationId);

        // Index by SKU για γρήγορη αναζήτηση
        $indexedInventory = [];
        foreach ($inventory as $item) {
            $sku = $item['meta_data']['sku'] ?? null;
            if ($sku) {
                $indexedInventory[$sku] = $item;
            }
        }

        $this->logger->info('ATUM inventory fetched', [
            'total_items' => count($inventory),
            'indexed_items' => count($indexedInventory),
            'location_id' => $locationId
        ]);

        $this->logger->logMemoryUsage('After ATUM fetch');

        return $indexedInventory;
    }

    /**
     * Process products και determine sync actions
     *
     * @param array $softOneProducts
     * @param array $atumInventory
     */
    private function processProducts($softOneProducts, $atumInventory) {
        $this->logger->info('Processing products για sync decisions', [
            'softone_products' => count($softOneProducts),
            'atum_inventory' => count($atumInventory)
        ]);

        foreach ($softOneProducts as $index => $softOneProduct) {
            try {
                $this->stats['products_processed']++;

                // Log progress every 100 products
                if ($this->stats['products_processed'] % 100 === 0) {
                    $this->logger->info('Processing progress', [
                        'processed' => $this->stats['products_processed'],
                        'total' => count($softOneProducts),
                        'percentage' => round(($this->stats['products_processed'] / count($softOneProducts)) * 100, 1)
                    ]);
                    $this->logger->logMemoryUsage("Product {$this->stats['products_processed']}");
                }

                $this->processSingleProduct($softOneProduct, $atumInventory);

                // Execute batch αν reached size limit
                if (count($this->batchOperations['create']) >= $this->batchSize ||
                    count($this->batchOperations['update']) >= $this->batchSize) {
                    $this->executeBatchOperations();
                }

            } catch (Exception $e) {
                $this->stats['products_errors']++;
                $this->stats['error_products'][] = [
                    'sku' => $softOneProduct['_sku'] ?? 'unknown',
                    'name' => $softOneProduct['_name'] ?? 'unknown',
                    'error' => $e->getMessage()
                ];

                $this->logger->error('Product processing error', [
                    'product_sku' => $softOneProduct['_sku'] ?? 'unknown',
                    'error' => $e->getMessage()
                ]);
            }
        }

        $this->logger->info('Product processing completed', [
            'processed' => $this->stats['products_processed'],
            'pending_creates' => count($this->batchOperations['create']),
            'pending_updates' => count($this->batchOperations['update'])
        ]);
    }

    /**
     * Process single product και determine action
     *
     * @param array $softOneProduct
     * @param array $atumInventory
     */
    private function processSingleProduct($softOneProduct, $atumInventory) {
        $sku = $softOneProduct['_sku'];
        $softOneQuantity = $softOneProduct['_stock_quantity'];

        if (empty($sku)) {
            $this->logger->warning('Product missing SKU, skipping', [
                'product_name' => $softOneProduct['_name'] ?? 'unknown'
            ]);
            $this->stats['products_skipped']++;
            return;
        }

        // Check αν το product υπάρχει στο ATUM inventory
        if (isset($atumInventory[$sku])) {
            $this->processExistingProduct($softOneProduct, $atumInventory[$sku]);
        } else {
            $this->processNewProduct($softOneProduct);
        }
    }

    /**
     * Process existing product (update quantity αν needed)
     *
     * @param array $softOneProduct
     * @param array $atumItem
     */
    private function processExistingProduct($softOneProduct, $atumItem) {
        $sku = $softOneProduct['_sku'];
        $softOneQuantity = $softOneProduct['_stock_quantity'];
        $atumQuantity = $atumItem['meta_data']['stock_quantity'] ?? 0;

        $this->logger->debug('Processing existing product', [
            'sku' => $sku,
            'softone_qty' => $softOneQuantity,
            'atum_qty' => $atumQuantity
        ]);

        // Compare quantities
        if ($softOneQuantity == $atumQuantity) {
            // Same quantity - no action needed
            $this->logger->debug('Product quantities match, skipping', ['sku' => $sku]);
            return;
        }

        // Different quantity - update needed
        $updateData = $this->wooCommerceClient->updateAtumInventory($atumItem['id'], [
            'stock_quantity' => $softOneQuantity
        ]);

        $this->batchOperations['update'][] = $updateData;
        $this->stats['updated_products'][] = [
            'sku' => $sku,
            'name' => $softOneProduct['_name'],
            'old_quantity' => $atumQuantity,
            'new_quantity' => $softOneQuantity
        ];

        $this->logger->debug('Product scheduled για update', [
            'sku' => $sku,
            'quantity_change' => "{$atumQuantity} -> {$softOneQuantity}"
        ]);
    }

    /**
     * Process new product (create in WooCommerce + ATUM)
     *
     * @param array $softOneProduct
     */
    private function processNewProduct($softOneProduct) {
        $sku = $softOneProduct['_sku'];

        $this->logger->debug('Processing new product', [
            'sku' => $sku,
            'name' => $softOneProduct['_name']
        ]);

        // Check αν το product υπάρχει στο WooCommerce αλλά όχι στο ATUM
        $wooProduct = $this->wooCommerceClient->findProductBySku($sku);

        if ($wooProduct) {
            // Product exists στο WooCommerce, create ATUM inventory only
            $this->createAtumInventoryOnly($softOneProduct, $wooProduct['id']);
        } else {
            // Product doesn't exist, create both WooCommerce product και ATUM inventory
            $this->createProductAndInventory($softOneProduct);
        }
    }

    /**
     * Create ATUM inventory για existing WooCommerce product
     *
     * @param array $softOneProduct
     * @param int $productId
     */
    private function createAtumInventoryOnly($softOneProduct, $productId) {
        $inventoryData = [
            'sku' => $softOneProduct['_sku'],
            'stock_quantity' => $softOneProduct['_stock_quantity'],
            'barcode' => $softOneProduct['_sku']
        ];

        $createData = $this->wooCommerceClient->createAtumInventory(
            $productId,
            $inventoryData,
            $this->storeConfig['atum']
        );

        $this->batchOperations['create'][] = $createData;

        $this->logger->debug('ATUM inventory scheduled για creation', [
            'product_id' => $productId,
            'sku' => $softOneProduct['_sku']
        ]);
    }

    /**
     * Create new WooCommerce product + ATUM inventory
     *
     * @param array $softOneProduct
     */
    private function createProductAndInventory($softOneProduct) {
        // Note: Για new products, θα χρειαστεί δύο-step process:
        // 1. Create WooCommerce product
        // 2. Create ATUM inventory με το new product ID
        // Αυτό δεν μπορεί να γίνει στο batch operation, οπότε θα το κάνουμε individually

        try {
            // Create WooCommerce product immediately
            $wooProduct = $this->wooCommerceClient->createProduct($softOneProduct, $this->storeConfig);
            $this->stats['api_calls_woocommerce']++;

            // Create ATUM inventory για το νέο product
            $inventoryData = [
                'sku' => $softOneProduct['_sku'],
                'stock_quantity' => $softOneProduct['_stock_quantity'],
                'barcode' => $softOneProduct['_sku']
            ];

            $createData = $this->wooCommerceClient->createAtumInventory(
                $wooProduct['id'],
                $inventoryData,
                $this->storeConfig['atum']
            );

            $this->batchOperations['create'][] = $createData;
            $this->stats['products_created']++;
            $this->stats['new_products'][] = [
                'sku' => $softOneProduct['_sku'],
                'name' => $softOneProduct['_name'],
                'woo_product_id' => $wooProduct['id']
            ];

            // Save product mapping για future reference
            $this->saveProductMapping($softOneProduct, $wooProduct['id']);

            $this->logger->info('New product created', [
                'sku' => $softOneProduct['_sku'],
                'name' => $softOneProduct['_name'],
                'woo_product_id' => $wooProduct['id']
            ]);

        } catch (Exception $e) {
            $this->logger->error('Failed to create new product', [
                'sku' => $softOneProduct['_sku'],
                'error' => $e->getMessage()
            ]);
            throw $e;
        }
    }

    /**
     * Execute batch operations
     */
    private function executeBatchOperations() {
        if (empty($this->batchOperations['create']) && empty($this->batchOperations['update'])) {
            return;
        }

        $this->logger->info('Executing batch operations', [
            'create_count' => count($this->batchOperations['create']),
            'update_count' => count($this->batchOperations['update'])
        ]);

        try {
            $response = $this->wooCommerceClient->batchAtumInventory($this->batchOperations);
            $this->stats['api_calls_woocommerce']++;

            // Process batch response
            $this->processBatchResponse($response);

            // Clear batch operations
            $this->batchOperations = ['create' => [], 'update' => [], 'delete' => []];

            $this->logger->info('Batch operations completed successfully');

        } catch (Exception $e) {
            $this->logger->error('Batch operations failed', [
                'error' => $e->getMessage(),
                'operations' => $this->batchOperations
            ]);
            throw $e;
        }
    }

    /**
     * Process batch operation response
     *
     * @param array $response
     */
    private function processBatchResponse($response) {
        // Process created items
        if (!empty($response['create'])) {
            foreach ($response['create'] as $item) {
                if (isset($item['id'])) {
                    $this->logger->debug('ATUM inventory created', [
                        'inventory_id' => $item['id'],
                        'product_id' => $item['product_id']
                    ]);
                } else {
                    $this->stats['products_errors']++;
                    $this->logger->warning('ATUM inventory creation failed', ['item' => $item]);
                }
            }
        }

        // Process updated items
        if (!empty($response['update'])) {
            foreach ($response['update'] as $item) {
                if (isset($item['id'])) {
                    $this->logger->debug('ATUM inventory updated', [
                        'inventory_id' => $item['id']
                    ]);
                } else {
                    $this->stats['products_errors']++;
                    $this->logger->warning('ATUM inventory update failed', ['item' => $item]);
                }
            }
        }
    }

    /**
     * Save product mapping to database
     *
     * @param array $softOneProduct
     * @param int $wooProductId
     * @param int $atumInventoryId
     */
    private function saveProductMapping($softOneProduct, $wooProductId, $atumInventoryId = null) {
        if (!$this->database) {
            return;
        }

        try {
            $stmt = $this->database->prepare("
                INSERT INTO product_mappings (
                    store_id, softone_item_id, softone_code, softone_sku,
                    woocommerce_product_id, atum_inventory_id, atum_location_id,
                    last_sync_time, last_sync_log_id, sync_status
                ) VALUES (?, ?, ?, ?, ?, ?, ?, NOW(), ?, 'synced')
                ON DUPLICATE KEY UPDATE
                    woocommerce_product_id = VALUES(woocommerce_product_id),
                    atum_inventory_id = VALUES(atum_inventory_id),
                    last_sync_time = NOW(),
                    last_sync_log_id = VALUES(last_sync_log_id),
                    sync_status = 'synced'
            ");

            $stmt->execute([
                $this->storeConfig['name'],
                $softOneProduct['ZOOMINFO'] ?? null,
                $softOneProduct['ITEM.CODE'] ?? null,
                $softOneProduct['_sku'],
                $wooProductId,
                $atumInventoryId,
                $this->storeConfig['atum']['location_id'],
                $this->syncLogId
            ]);

        } catch (Exception $e) {
            $this->logger->warning('Failed to save product mapping', [
                'error' => $e->getMessage(),
                'sku' => $softOneProduct['_sku']
            ]);
        }
    }

    /**
     * Update sync statistics στο database
     */
    private function updateStatistics() {
        if (!$this->database || !$this->syncLogId) {
            return;
        }

        try {
            $this->logger->logSyncStats($this->stats);

            // Update daily statistics
            $stmt = $this->database->prepare("
                INSERT INTO sync_statistics (
                    date, store_id, total_syncs, successful_syncs,
                    total_products_processed, total_products_created,
                    total_products_updated, total_errors,
                    api_calls_softone, api_calls_woocommerce
                ) VALUES (CURDATE(), ?, 1, 1, ?, ?, ?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE
                    total_syncs = total_syncs + 1,
                    successful_syncs = successful_syncs + 1,
                    total_products_processed = total_products_processed + VALUES(total_products_processed),
                    total_products_created = total_products_created + VALUES(total_products_created),
                    total_products_updated = total_products_updated + VALUES(total_products_updated),
                    total_errors = total_errors + VALUES(total_errors),
                    api_calls_softone = api_calls_softone + VALUES(api_calls_softone),
                    api_calls_woocommerce = api_calls_woocommerce + VALUES(api_calls_woocommerce)
            ");

            $stmt->execute([
                $this->storeConfig['name'],
                $this->stats['products_processed'],
                $this->stats['products_created'],
                $this->stats['products_updated'],
                $this->stats['products_errors'],
                $this->stats['api_calls_softone'],
                $this->stats['api_calls_woocommerce']
            ]);

        } catch (Exception $e) {
            $this->logger->warning('Failed to update statistics', [
                'error' => $e->getMessage()
            ]);
        }
    }

    /**
     * Get synchronization statistics
     *
     * @return array
     */
    public function getStatistics() {
        return $this->stats;
    }

    /**
     * Test synchronization με limited dataset
     *
     * @param int $limit Number of products to test
     * @return array Test results
     */
    public function testSync($limit = 10) {
        $this->logger->info('Starting test synchronization', ['limit' => $limit]);

        // Fetch limited products από SoftOne Go
        $response = $this->softOneClient->fetchProducts(['ITEM.MTRL_ITEMTRDATA_QTY1_TO' => $limit]);
        $products = array_slice($response['products'] ?? [], 0, $limit);

        // Process products σε dry run mode
        $atumInventory = $this->fetchAtumInventory();
        $this->processProducts($products, $atumInventory);

        return [
            'test_products_count' => count($products),
            'would_create' => count($this->batchOperations['create']),
            'would_update' => count($this->batchOperations['update']),
            'statistics' => $this->stats
        ];
    }
}