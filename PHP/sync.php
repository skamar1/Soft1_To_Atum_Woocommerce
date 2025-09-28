<?php

/**
 * Main Synchronization Script
 * Κύριο script για συγχρονισμό SoftOne Go με ATUM
 *
 * Usage:
 * php sync.php [options]
 *
 * Options:
 * --store=store_id     Sync specific store only
 * --dry-run           Run sync without making changes
 * --test              Run test sync με limited products
 * --verbose           Enable verbose logging
 * --help              Show help message
 */

// Error reporting
error_reporting(E_ALL);
ini_set('display_errors', 0); // Disable για production

// Include required classes
require_once __DIR__ . '/src/Logger.php';
require_once __DIR__ . '/src/SoftOneGoClient.php';
require_once __DIR__ . '/src/WooCommerceClient.php';
require_once __DIR__ . '/src/ProductSynchronizer.php';
require_once __DIR__ . '/src/EmailNotifier.php';

class SyncApplication {

    private $config;
    private $storesConfig;
    private $dbConfig;
    private $database;
    private $logger;
    private $emailNotifier;
    private $lockFile;
    private $startTime;

    // Command line options
    private $options = [
        'store' => null,
        'dry-run' => false,
        'test' => false,
        'verbose' => false,
        'help' => false
    ];

    /**
     * Constructor
     */
    public function __construct() {
        $this->startTime = microtime(true);

        // Load configurations
        $this->loadConfigurations();

        // Parse command line arguments
        $this->parseArguments();

        // Show help αν requested
        if ($this->options['help']) {
            $this->showHelp();
            exit(0);
        }

        // Setup lock file
        $this->lockFile = $this->config['lock_file'];

        // Initialize database connection
        $this->initializeDatabase();

        // Initialize logger
        $this->initializeLogger();

        // Initialize email notifier
        $this->emailNotifier = new EmailNotifier($this->config, $this->logger, $this->database);

        $this->logger->info('SyncApplication initialized', [
            'options' => $this->options,
            'pid' => getmypid(),
            'memory_limit' => ini_get('memory_limit'),
            'execution_time_limit' => ini_get('max_execution_time')
        ]);
    }

    /**
     * Main execution method
     */
    public function run() {
        try {
            // Check και acquire lock
            $this->acquireLock();

            // Set memory και time limits
            $this->setLimits();

            // Run synchronization
            $results = $this->runSynchronization();

            // Send notifications
            $this->sendNotifications($results);

            // Cleanup
            $this->cleanup();

            $executionTime = microtime(true) - $this->startTime;
            $this->logger->info('Sync application completed successfully', [
                'total_execution_time' => round($executionTime, 2) . 's',
                'peak_memory_usage' => $this->formatBytes(memory_get_peak_usage(true)),
                'results_summary' => $this->summarizeResults($results)
            ]);

            exit(0);

        } catch (Exception $e) {
            $this->logger->error('Sync application failed', [
                'error' => $e->getMessage(),
                'trace' => $e->getTraceAsString()
            ]);

            // Send error notification
            if ($this->emailNotifier) {
                $this->emailNotifier->sendErrorNotification($e->getMessage(), [
                    'options' => $this->options,
                    'execution_time' => microtime(true) - $this->startTime
                ]);
            }

            $this->cleanup();
            exit(1);
        }
    }

    /**
     * Load configuration files
     */
    private function loadConfigurations() {
        // Load app config
        $configFile = __DIR__ . '/config/app.php';
        if (!file_exists($configFile)) {
            throw new Exception("Configuration file not found: {$configFile}");
        }
        $this->config = require $configFile;

        // Load stores config
        $storesFile = __DIR__ . '/config/stores.php';
        if (!file_exists($storesFile)) {
            throw new Exception("Stores configuration file not found: {$storesFile}");
        }
        $this->storesConfig = require $storesFile;

        // Load database config
        $dbFile = __DIR__ . '/config/database.php';
        if (!file_exists($dbFile)) {
            throw new Exception("Database configuration file not found: {$dbFile}");
        }
        $this->dbConfig = require $dbFile;

        // Load .env file αν υπάρχει
        $envFile = __DIR__ . '/.env';
        if (file_exists($envFile)) {
            $this->loadEnvFile($envFile);
        }
    }

    /**
     * Load environment variables από .env file
     */
    private function loadEnvFile($envFile) {
        $lines = file($envFile, FILE_IGNORE_NEW_LINES | FILE_SKIP_EMPTY_LINES);
        foreach ($lines as $line) {
            if (strpos($line, '#') === 0) continue; // Skip comments

            list($key, $value) = explode('=', $line, 2);
            $key = trim($key);
            $value = trim($value);

            // Remove quotes αν υπάρχουν
            if (preg_match('/^"(.*)"$/', $value, $matches)) {
                $value = $matches[1];
            }

            $_ENV[$key] = $value;
            putenv("{$key}={$value}");
        }
    }

    /**
     * Parse command line arguments
     */
    private function parseArguments() {
        $longOptions = [
            'store:',
            'dry-run',
            'test',
            'verbose',
            'help'
        ];

        $opts = getopt('', $longOptions);

        $this->options['store'] = $opts['store'] ?? null;
        $this->options['dry-run'] = isset($opts['dry-run']);
        $this->options['test'] = isset($opts['test']);
        $this->options['verbose'] = isset($opts['verbose']);
        $this->options['help'] = isset($opts['help']);
    }

    /**
     * Initialize database connection
     */
    private function initializeDatabase() {
        try {
            $dbConfig = $this->dbConfig['default'];

            $dsn = "mysql:host={$dbConfig['host']};port={$dbConfig['port']};dbname={$dbConfig['database']};charset={$dbConfig['charset']}";

            $this->database = new PDO($dsn, $dbConfig['username'], $dbConfig['password'], $dbConfig['options']);

            // Test connection
            $this->database->query('SELECT 1');

        } catch (Exception $e) {
            throw new Exception("Database connection failed: " . $e->getMessage());
        }
    }

    /**
     * Initialize logger
     */
    private function initializeLogger() {
        try {
            // Create sync log entry
            $syncLogId = $this->createSyncLogEntry();

            // Create logger με sync log ID
            $this->logger = new Logger($this->config, $this->database, $syncLogId);

            // Set verbose mode αν requested
            if ($this->options['verbose']) {
                $this->config['debug'] = true;
                $this->config['log_level'] = 'DEBUG';
            }

        } catch (Exception $e) {
            throw new Exception("Logger initialization failed: " . $e->getMessage());
        }
    }

    /**
     * Create sync log entry στο database
     */
    private function createSyncLogEntry() {
        try {
            $syncType = 'automatic';
            if ($this->options['test']) {
                $syncType = 'test';
            } elseif (!empty($_SERVER['HTTP_HOST']) || !empty($_SERVER['REQUEST_URI'])) {
                $syncType = 'manual';
            }

            $stmt = $this->database->prepare("
                INSERT INTO sync_logs (store_id, sync_type, start_time, status)
                VALUES (?, ?, NOW(), 'running')
            ");

            $storeId = $this->options['store'] ?? 'all';
            $stmt->execute([$storeId, $syncType]);

            return $this->database->lastInsertId();

        } catch (Exception $e) {
            // Return null αν database logging fails
            error_log("Failed to create sync log entry: " . $e->getMessage());
            return null;
        }
    }

    /**
     * Acquire lock file
     */
    private function acquireLock() {
        if (file_exists($this->lockFile)) {
            $lockAge = time() - filemtime($this->lockFile);
            $lockTimeout = $this->config['lock_timeout'] ?? 3600;

            if ($lockAge < $lockTimeout) {
                throw new Exception("Another sync process is running (lock file: {$this->lockFile})");
            } else {
                // Remove stale lock file
                unlink($this->lockFile);
                $this->logger->warning('Removed stale lock file', ['lock_age' => $lockAge]);
            }
        }

        // Create lock file
        if (!file_put_contents($this->lockFile, getmypid())) {
            throw new Exception("Cannot create lock file: {$this->lockFile}");
        }

        $this->logger->debug('Lock acquired', ['lock_file' => $this->lockFile]);
    }

    /**
     * Set memory και execution time limits
     */
    private function setLimits() {
        // Set memory limit
        $memoryLimit = $this->config['memory_limit'] ?? '512M';
        ini_set('memory_limit', $memoryLimit);

        // Set execution time limit
        $timeLimit = $this->config['execution_time_limit'] ?? 3600;
        set_time_limit($timeLimit);

        $this->logger->debug('Limits set', [
            'memory_limit' => $memoryLimit,
            'time_limit' => $timeLimit
        ]);
    }

    /**
     * Run synchronization για all enabled stores
     */
    private function runSynchronization() {
        $results = [];
        $storesToSync = $this->getStoresToSync();

        $this->logger->info('Starting synchronization', [
            'stores_to_sync' => array_keys($storesToSync),
            'dry_run' => $this->options['dry-run'],
            'test_mode' => $this->options['test']
        ]);

        foreach ($storesToSync as $storeId => $storeConfig) {
            try {
                $this->logger->info("Starting sync για store: {$storeId}");

                // Create synchronizer
                $syncLogId = $this->logger->syncLogId ?? null;
                $synchronizer = new ProductSynchronizer($storeConfig, $this->logger, $this->database, $syncLogId);

                // Run sync
                if ($this->options['test']) {
                    $result = $synchronizer->testSync(10);
                } else {
                    $result = $synchronizer->synchronize($this->options['dry-run']);
                }

                $result['store_id'] = $storeId;
                $result['store_name'] = $storeConfig['name'];
                $results[$storeId] = $result;

                $this->logger->info("Completed sync για store: {$storeId}", [
                    'success' => $result['success'],
                    'statistics' => $result['statistics'] ?? []
                ]);

            } catch (Exception $e) {
                $results[$storeId] = [
                    'store_id' => $storeId,
                    'store_name' => $storeConfig['name'],
                    'success' => false,
                    'error' => $e->getMessage()
                ];

                $this->logger->error("Sync failed για store: {$storeId}", [
                    'error' => $e->getMessage()
                ]);
            }

            // Memory cleanup between stores
            if (function_exists('gc_collect_cycles')) {
                gc_collect_cycles();
            }

            $this->logger->logMemoryUsage("After store {$storeId}");
        }

        return $results;
    }

    /**
     * Get stores to synchronize
     */
    private function getStoresToSync() {
        $storesToSync = [];

        if ($this->options['store']) {
            // Specific store requested
            $storeId = $this->options['store'];
            if (!isset($this->storesConfig[$storeId])) {
                throw new Exception("Store not found: {$storeId}");
            }
            $storesToSync[$storeId] = $this->storesConfig[$storeId];
        } else {
            // All enabled stores
            foreach ($this->storesConfig as $storeId => $storeConfig) {
                if ($storeConfig['enabled'] ?? true) {
                    $storesToSync[$storeId] = $storeConfig;
                }
            }
        }

        if (empty($storesToSync)) {
            throw new Exception("No stores enabled για synchronization");
        }

        return $storesToSync;
    }

    /**
     * Send notifications based on results
     */
    private function sendNotifications($results) {
        foreach ($results as $storeId => $result) {
            if (!$result['success']) {
                continue;
            }

            $stats = $result['statistics'] ?? [];

            // Send new products notification
            if (!empty($stats['new_products'])) {
                $this->emailNotifier->sendNewProductsNotification(
                    $stats['new_products'],
                    $stats,
                    $this->logger->syncLogId ?? null
                );
            }
        }
    }

    /**
     * Cleanup resources
     */
    private function cleanup() {
        // Remove lock file
        if (file_exists($this->lockFile)) {
            unlink($this->lockFile);
            $this->logger->debug('Lock released');
        }

        // Update sync log
        $this->updateSyncLogEntry();

        // Process email queue
        if ($this->emailNotifier) {
            $this->emailNotifier->processEmailQueue(5);
        }

        // Clean old logs
        if ($this->logger) {
            $this->logger->cleanupOldLogs();
        }
    }

    /**
     * Update sync log entry με completion
     */
    private function updateSyncLogEntry() {
        if (!$this->database || !$this->logger->syncLogId) {
            return;
        }

        try {
            $stmt = $this->database->prepare("
                UPDATE sync_logs
                SET end_time = NOW(),
                    status = 'completed',
                    execution_time = ?
                WHERE id = ?
            ");

            $executionTime = round(microtime(true) - $this->startTime);
            $stmt->execute([$executionTime, $this->logger->syncLogId]);

        } catch (Exception $e) {
            error_log("Failed to update sync log: " . $e->getMessage());
        }
    }

    /**
     * Summarize results για logging
     */
    private function summarizeResults($results) {
        $summary = [
            'total_stores' => count($results),
            'successful_stores' => 0,
            'failed_stores' => 0,
            'total_products_processed' => 0,
            'total_products_created' => 0,
            'total_products_updated' => 0
        ];

        foreach ($results as $result) {
            if ($result['success']) {
                $summary['successful_stores']++;
                $stats = $result['statistics'] ?? [];
                $summary['total_products_processed'] += $stats['products_processed'] ?? 0;
                $summary['total_products_created'] += $stats['products_created'] ?? 0;
                $summary['total_products_updated'] += $stats['products_updated'] ?? 0;
            } else {
                $summary['failed_stores']++;
            }
        }

        return $summary;
    }

    /**
     * Format bytes για human readable output
     */
    private function formatBytes($bytes, $precision = 2) {
        $units = ['B', 'KB', 'MB', 'GB', 'TB'];

        for ($i = 0; $bytes > 1024 && $i < count($units) - 1; $i++) {
            $bytes /= 1024;
        }

        return round($bytes, $precision) . ' ' . $units[$i];
    }

    /**
     * Show help message
     */
    private function showHelp() {
        echo "SoftOne Go to ATUM Synchronization Script\n";
        echo "=========================================\n\n";
        echo "Usage: php sync.php [options]\n\n";
        echo "Options:\n";
        echo "  --store=STORE_ID    Sync specific store only\n";
        echo "  --dry-run          Run sync without making changes\n";
        echo "  --test             Run test sync με limited products\n";
        echo "  --verbose          Enable verbose logging\n";
        echo "  --help             Show this help message\n\n";
        echo "Examples:\n";
        echo "  php sync.php                     # Sync all enabled stores\n";
        echo "  php sync.php --store=store1      # Sync specific store\n";
        echo "  php sync.php --dry-run           # Test run without changes\n";
        echo "  php sync.php --test --verbose    # Test με verbose output\n\n";
    }
}

// Run application
try {
    $app = new SyncApplication();
    $app->run();
} catch (Exception $e) {
    error_log("Sync application startup failed: " . $e->getMessage());
    exit(1);
}