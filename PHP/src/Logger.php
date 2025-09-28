<?php

/**
 * Logger Class
 * Comprehensive logging system για SoftOne Go to ATUM Sync
 *
 * Features:
 * - Multi-level logging (DEBUG, INFO, WARNING, ERROR)
 * - Daily log rotation
 * - Memory usage tracking
 * - Performance timing
 * - Email alerts για critical errors
 * - Database logging για important events
 */

class Logger {

    const DEBUG = 1;
    const INFO = 2;
    const WARNING = 3;
    const ERROR = 4;

    private $logLevels = [
        self::DEBUG => 'DEBUG',
        self::INFO => 'INFO',
        self::WARNING => 'WARNING',
        self::ERROR => 'ERROR'
    ];

    private $config;
    private $logPath;
    private $currentLogLevel;
    private $fileHandles = [];
    private $startTime;
    private $lastMemoryUsage;
    private $database;
    private $syncLogId;

    /**
     * Constructor
     *
     * @param array $config Configuration από app.php
     * @param PDO $database Database connection (optional)
     * @param int $syncLogId Current sync log ID για database tracking
     */
    public function __construct($config, $database = null, $syncLogId = null) {
        $this->config = $config;
        $this->database = $database;
        $this->syncLogId = $syncLogId;
        $this->logPath = dirname(__DIR__) . '/logs';
        $this->startTime = microtime(true);
        $this->lastMemoryUsage = memory_get_usage(true);

        // Set log level από configuration
        $levelMap = [
            'DEBUG' => self::DEBUG,
            'INFO' => self::INFO,
            'WARNING' => self::WARNING,
            'ERROR' => self::ERROR
        ];
        $this->currentLogLevel = $levelMap[$config['log_level']] ?? self::INFO;

        // Create logs directory αν δεν υπάρχει
        if (!is_dir($this->logPath)) {
            mkdir($this->logPath, 0777, true);
        }

        // Initialize log files
        $this->initializeLogFiles();

        // Log session start
        $this->info('Logger initialized', [
            'log_level' => $config['log_level'],
            'log_path' => $this->logPath,
            'sync_log_id' => $syncLogId
        ]);
    }

    /**
     * Log debug message
     */
    public function debug($message, $context = []) {
        $this->log(self::DEBUG, $message, $context);
    }

    /**
     * Log info message
     */
    public function info($message, $context = []) {
        $this->log(self::INFO, $message, $context);
    }

    /**
     * Log warning message
     */
    public function warning($message, $context = []) {
        $this->log(self::WARNING, $message, $context);
    }

    /**
     * Log error message
     */
    public function error($message, $context = [], $sendEmail = true) {
        $this->log(self::ERROR, $message, $context);

        // Send email για critical errors
        if ($sendEmail && $this->config['email_on_error'] ?? false) {
            $this->sendErrorEmail($message, $context);
        }
    }

    /**
     * Log performance timing
     */
    public function logTiming($operation, $startTime, $context = []) {
        $duration = (microtime(true) - $startTime) * 1000; // milliseconds
        $this->info("Performance: {$operation}", array_merge($context, [
            'duration_ms' => round($duration, 2),
            'memory_usage' => $this->formatBytes(memory_get_usage(true)),
            'memory_peak' => $this->formatBytes(memory_get_peak_usage(true))
        ]));
    }

    /**
     * Log memory usage
     */
    public function logMemoryUsage($checkpoint = '') {
        $currentMemory = memory_get_usage(true);
        $peakMemory = memory_get_peak_usage(true);
        $memoryDiff = $currentMemory - $this->lastMemoryUsage;

        $this->debug("Memory checkpoint: {$checkpoint}", [
            'current_memory' => $this->formatBytes($currentMemory),
            'peak_memory' => $this->formatBytes($peakMemory),
            'memory_diff' => $this->formatBytes($memoryDiff),
            'memory_diff_sign' => $memoryDiff >= 0 ? '+' : ''
        ]);

        $this->lastMemoryUsage = $currentMemory;
    }

    /**
     * Log API call
     */
    public function logApiCall($service, $endpoint, $method, $duration, $statusCode, $context = []) {
        $level = $statusCode >= 400 ? self::WARNING : self::INFO;
        $this->log($level, "API Call: {$service}", array_merge($context, [
            'endpoint' => $endpoint,
            'method' => $method,
            'duration_ms' => round($duration, 2),
            'status_code' => $statusCode,
            'service' => $service
        ]));
    }

    /**
     * Log sync statistics
     */
    public function logSyncStats($stats) {
        $this->info('Sync Statistics', $stats);

        // Also log to database αν available
        if ($this->database && $this->syncLogId) {
            $this->updateSyncLogStats($stats);
        }
    }

    /**
     * Main logging method
     */
    private function log($level, $message, $context = []) {
        // Check αν το level είναι enabled
        if ($level < $this->currentLogLevel) {
            return;
        }

        $timestamp = date('Y-m-d H:i:s');
        $levelName = $this->logLevels[$level];
        $contextStr = empty($context) ? '' : ' ' . json_encode($context, JSON_UNESCAPED_UNICODE);

        // Format log entry
        $logEntry = "[{$timestamp}] {$levelName}: {$message}{$contextStr}" . PHP_EOL;

        // Write to appropriate log files
        $this->writeToFile('all', $logEntry);

        if ($level >= self::WARNING) {
            $this->writeToFile('error', $logEntry);
        }

        // Write to database για important events
        if ($level >= self::INFO && $this->database) {
            $this->writeToDatabase($level, $message, $context);
        }

        // Output to console αν debug mode
        if ($this->config['debug'] ?? false) {
            echo $logEntry;
        }
    }

    /**
     * Initialize log files για daily rotation
     */
    private function initializeLogFiles() {
        $date = date('Y-m-d');

        $logFiles = [
            'all' => "sync_{$date}.log",
            'error' => "error_{$date}.log"
        ];

        foreach ($logFiles as $type => $filename) {
            $filepath = $this->logPath . '/' . $filename;
            $this->fileHandles[$type] = fopen($filepath, 'a');

            if (!$this->fileHandles[$type]) {
                throw new Exception("Cannot open log file: {$filepath}");
            }
        }
    }

    /**
     * Write to specific log file
     */
    private function writeToFile($type, $logEntry) {
        if (isset($this->fileHandles[$type])) {
            fwrite($this->fileHandles[$type], $logEntry);
            fflush($this->fileHandles[$type]);
        }
    }

    /**
     * Write important events to database
     */
    private function writeToDatabase($level, $message, $context) {
        try {
            if (!$this->database || !$this->syncLogId) {
                return;
            }

            // Log only για specific events
            $importantEvents = ['sync_start', 'sync_complete', 'sync_error', 'api_error', 'product_created'];
            $isImportant = false;

            foreach ($importantEvents as $event) {
                if (strpos(strtolower($message), $event) !== false) {
                    $isImportant = true;
                    break;
                }
            }

            if (!$isImportant) {
                return;
            }

            // Insert database log (simplified - θα χρειαστεί ξεχωριστός πίνακας)
            $stmt = $this->database->prepare("
                UPDATE sync_logs
                SET error_message = CONCAT(IFNULL(error_message, ''), ?, '\n')
                WHERE id = ?
            ");

            $logMessage = "[" . $this->logLevels[$level] . "] " . $message;
            if (!empty($context)) {
                $logMessage .= " | " . json_encode($context, JSON_UNESCAPED_UNICODE);
            }

            $stmt->execute([$logMessage, $this->syncLogId]);

        } catch (Exception $e) {
            // Δεν πρέπει να crash το logging system
            error_log("Logger database error: " . $e->getMessage());
        }
    }

    /**
     * Update sync log με statistics
     */
    private function updateSyncLogStats($stats) {
        try {
            if (!$this->database || !$this->syncLogId) {
                return;
            }

            $stmt = $this->database->prepare("
                UPDATE sync_logs SET
                    products_processed = ?,
                    products_created = ?,
                    products_updated = ?,
                    products_errors = ?,
                    memory_usage = ?,
                    summary_data = ?
                WHERE id = ?
            ");

            $stmt->execute([
                $stats['products_processed'] ?? 0,
                $stats['products_created'] ?? 0,
                $stats['products_updated'] ?? 0,
                $stats['products_errors'] ?? 0,
                $this->formatBytes(memory_get_peak_usage(true)),
                json_encode($stats, JSON_UNESCAPED_UNICODE),
                $this->syncLogId
            ]);

        } catch (Exception $e) {
            error_log("Logger stats update error: " . $e->getMessage());
        }
    }

    /**
     * Send error email notification
     */
    private function sendErrorEmail($message, $context) {
        try {
            // Queue email for sending (θα υλοποιηθεί στο EmailNotifier)
            if ($this->database) {
                $stmt = $this->database->prepare("
                    INSERT INTO email_queue (sync_log_id, email_type, recipient, subject, body, scheduled_at)
                    VALUES (?, 'error_notification', ?, ?, ?, NOW())
                ");

                $recipients = $this->config['email_recipients'] ?? ['admin@example.com'];
                $subject = "Sync Error - " . ($this->config['app_name'] ?? 'SoftOne ATUM Sync');
                $body = "Error: {$message}\n\nContext: " . json_encode($context, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);

                foreach ($recipients as $recipient) {
                    $stmt->execute([$this->syncLogId, $recipient, $subject, $body]);
                }
            }
        } catch (Exception $e) {
            error_log("Logger email queue error: " . $e->getMessage());
        }
    }

    /**
     * Cleanup old log files
     */
    public function cleanupOldLogs() {
        $retentionDays = $this->config['log_retention_days'] ?? 30;
        $cutoffDate = date('Y-m-d', strtotime("-{$retentionDays} days"));

        $files = glob($this->logPath . '/*.log');
        foreach ($files as $file) {
            $filename = basename($file);

            // Extract date από filename (format: sync_2025-09-27.log)
            if (preg_match('/(\d{4}-\d{2}-\d{2})\.log$/', $filename, $matches)) {
                $fileDate = $matches[1];
                if ($fileDate < $cutoffDate) {
                    unlink($file);
                    $this->info("Deleted old log file: {$filename}");
                }
            }
        }
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
     * Get execution time από start
     */
    public function getExecutionTime() {
        return microtime(true) - $this->startTime;
    }

    /**
     * Close log files
     */
    public function __destruct() {
        $executionTime = $this->getExecutionTime();
        $this->info('Logger session ended', [
            'total_execution_time' => round($executionTime, 2) . 's',
            'peak_memory_usage' => $this->formatBytes(memory_get_peak_usage(true))
        ]);

        foreach ($this->fileHandles as $handle) {
            if (is_resource($handle)) {
                fclose($handle);
            }
        }
    }

    /**
     * Create logger instance από configuration
     */
    public static function create($configPath = null, $database = null, $syncLogId = null) {
        $configPath = $configPath ?? dirname(__DIR__) . '/config/app.php';

        if (!file_exists($configPath)) {
            throw new Exception("Configuration file not found: {$configPath}");
        }

        $config = require $configPath;
        return new self($config, $database, $syncLogId);
    }
}